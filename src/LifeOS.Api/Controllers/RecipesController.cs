using System.Text;
using LifeOS.Application.Interfaces;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RecipesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IGoogleDocsService _googleDocs;
    private readonly ILogger<RecipesController> _logger;

    public RecipesController(AppDbContext context, IGoogleDocsService googleDocs, ILogger<RecipesController> logger)
    {
        _context = context;
        _googleDocs = googleDocs;
        _logger = logger;
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(sub!);
    }

    // ==================== RECIPES ====================

    [HttpGet]
    public async Task<ActionResult<List<RecipeDto>>> GetRecipes(CancellationToken ct)
    {
        var userId = GetUserId();
        var recipes = await _context.Recipes
            .AsNoTracking()
            .Include(r => r.Ingredients)
            .Where(r => r.UserId == userId)
            .OrderBy(r => r.Name)
            .ToListAsync(ct);

        return Ok(recipes.Select(MapRecipe));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RecipeDto>> GetRecipe(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var recipe = await _context.Recipes
            .AsNoTracking()
            .Include(r => r.Ingredients)
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);

        if (recipe == null) return NotFound();
        return Ok(MapRecipe(recipe));
    }

    [HttpPost]
    public async Task<ActionResult<RecipeDto>> CreateRecipe(SaveRecipeRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var recipe = new Recipe
        {
            UserId = userId,
            Name = request.Name,
            Description = request.Description,
            Category = request.Category,
            BaseServings = request.BaseServings > 0 ? request.BaseServings : 4,
            PrepTime = request.PrepTime,
            CookTime = request.CookTime,
            Instructions = request.Instructions,
            Ingredients = request.Ingredients.Select((ing, i) => new RecipeIngredient
            {
                Name = ing.Name,
                Quantity = ing.Quantity,
                Unit = ing.Unit,
                Notes = ing.Notes,
                SortOrder = i
            }).ToList()
        };

        _context.Recipes.Add(recipe);
        await _context.SaveChangesAsync(ct);
        await MaybeAutoSync(userId, ct);
        return Ok(MapRecipe(recipe));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RecipeDto>> UpdateRecipe(Guid id, SaveRecipeRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var recipe = await _context.Recipes
            .Include(r => r.Ingredients)
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);

        if (recipe == null) return NotFound();

        recipe.Name = request.Name;
        recipe.Description = request.Description;
        recipe.Category = request.Category;
        recipe.BaseServings = request.BaseServings > 0 ? request.BaseServings : recipe.BaseServings;
        recipe.PrepTime = request.PrepTime;
        recipe.CookTime = request.CookTime;
        recipe.Instructions = request.Instructions;
        recipe.UpdatedAt = DateTimeOffset.UtcNow;

        // Replace ingredients: remove old ones, then explicitly ADD new ones
        // (assigning via navigation with pre-set Guids makes EF issue UPDATEs for non-existent rows)
        _context.RecipeIngredients.RemoveRange(recipe.Ingredients);
        var newIngredients = request.Ingredients.Select((ing, i) => new RecipeIngredient
        {
            RecipeId = recipe.Id,
            Name = ing.Name,
            Quantity = ing.Quantity,
            Unit = ing.Unit,
            Notes = ing.Notes,
            SortOrder = i
        }).ToList();
        _context.RecipeIngredients.AddRange(newIngredients);

        await _context.SaveChangesAsync(ct);
        recipe.Ingredients = newIngredients;
        await MaybeAutoSync(userId, ct);
        return Ok(MapRecipe(recipe));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteRecipe(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var recipe = await _context.Recipes
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);

        if (recipe == null) return NotFound();

        _context.Recipes.Remove(recipe);
        await _context.SaveChangesAsync(ct);
        await MaybeAutoSync(userId, ct);
        return NoContent();
    }

    // ==================== SETTINGS ====================

    [HttpGet("settings")]
    public async Task<ActionResult<RecipeSettingsDto>> GetSettings(CancellationToken ct)
    {
        var userId = GetUserId();
        var settings = await _context.RecipeSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);

        return Ok(new RecipeSettingsDto
        {
            GoogleDocId = settings?.GoogleDocId,
            HasServiceAccount = _googleDocs.IsConfigured,
            ServiceAccountEmail = _googleDocs.ServiceAccountEmail,
            AutoSync = settings?.AutoSync ?? false,
            LastSyncAt = settings?.LastSyncAt
        });
    }

    [HttpPut("settings")]
    public async Task<ActionResult<RecipeSettingsDto>> SaveSettings(SaveRecipeSettingsRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var settings = await _context.RecipeSettings
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);

        if (settings == null)
        {
            settings = new RecipeSettings { UserId = userId };
            _context.RecipeSettings.Add(settings);
        }

        settings.GoogleDocId = request.GoogleDocId;
        settings.AutoSync = request.AutoSync;
        settings.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(ct);

        return Ok(new RecipeSettingsDto
        {
            GoogleDocId = settings.GoogleDocId,
            HasServiceAccount = _googleDocs.IsConfigured,
            ServiceAccountEmail = _googleDocs.ServiceAccountEmail,
            AutoSync = settings.AutoSync,
            LastSyncAt = settings.LastSyncAt
        });
    }

    // ==================== SYNC ====================

    [HttpPost("sync/test")]
    public async Task<ActionResult<object>> TestSync(CancellationToken ct)
    {
        var userId = GetUserId();
        var settings = await _context.RecipeSettings.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId, ct);
        if (string.IsNullOrWhiteSpace(settings?.GoogleDocId))
            return BadRequest(new ProblemDetails { Title = "Not configured", Detail = "Set your Google Doc ID first." });
        if (!_googleDocs.IsConfigured)
            return BadRequest(new ProblemDetails { Title = "Service account missing", Detail = "The server has no Google service account key configured." });

        try
        {
            var title = await _googleDocs.TestConnectionAsync(settings.GoogleDocId, ct);
            return Ok(new { success = true, documentTitle = title });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google Docs test connection failed");
            return BadRequest(new ProblemDetails { Title = "Connection failed", Detail = ex.Message });
        }
    }

    [HttpPost("sync")]
    public async Task<ActionResult<object>> Sync(CancellationToken ct)
    {
        var userId = GetUserId();
        var settings = await _context.RecipeSettings.FirstOrDefaultAsync(s => s.UserId == userId, ct);
        if (string.IsNullOrWhiteSpace(settings?.GoogleDocId))
            return BadRequest(new ProblemDetails { Title = "Not configured", Detail = "Set your Google Doc ID first." });
        if (!_googleDocs.IsConfigured)
            return BadRequest(new ProblemDetails { Title = "Service account missing", Detail = "The server has no Google service account key configured." });

        var recipes = await _context.Recipes
            .AsNoTracking()
            .Include(r => r.Ingredients)
            .Where(r => r.UserId == userId)
            .OrderBy(r => r.Name)
            .ToListAsync(ct);

        var content = BuildRecipeDocument(recipes);

        try
        {
            await _googleDocs.SyncContentAsync(settings.GoogleDocId, content, ct);
            settings.LastSyncAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(ct);
            return Ok(new { success = true, recipesSynced = recipes.Count, syncedAt = settings.LastSyncAt });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google Docs sync failed");
            return BadRequest(new ProblemDetails { Title = "Sync failed", Detail = ex.Message });
        }
    }

    private async Task MaybeAutoSync(Guid userId, CancellationToken ct)
    {
        try
        {
            var settings = await _context.RecipeSettings.FirstOrDefaultAsync(s => s.UserId == userId, ct);
            if (settings?.AutoSync != true || string.IsNullOrWhiteSpace(settings.GoogleDocId) || !_googleDocs.IsConfigured)
                return;

            var recipes = await _context.Recipes.AsNoTracking().Include(r => r.Ingredients)
                .Where(r => r.UserId == userId).OrderBy(r => r.Name).ToListAsync(ct);
            await _googleDocs.SyncContentAsync(settings.GoogleDocId, BuildRecipeDocument(recipes), ct);
            settings.LastSyncAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Auto-sync failed (non-fatal)");
        }
    }

    private static string BuildRecipeDocument(List<Recipe> recipes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("LifeOS Recipe Book");
        sb.AppendLine($"Last synced: {DateTimeOffset.UtcNow:MMMM d, yyyy h:mm tt} UTC");
        sb.AppendLine();
        sb.AppendLine(new string('=', 50));

        foreach (var r in recipes)
        {
            sb.AppendLine();
            sb.AppendLine(r.Name.ToUpperInvariant());
            if (!string.IsNullOrWhiteSpace(r.Category)) sb.AppendLine($"Category: {r.Category}");
            if (!string.IsNullOrWhiteSpace(r.Description)) sb.AppendLine(r.Description);
            var times = new List<string>();
            if (!string.IsNullOrWhiteSpace(r.PrepTime)) times.Add($"Prep: {r.PrepTime}");
            if (!string.IsNullOrWhiteSpace(r.CookTime)) times.Add($"Cook: {r.CookTime}");
            times.Add($"Servings: {r.BaseServings}");
            sb.AppendLine(string.Join("  |  ", times));
            sb.AppendLine();
            sb.AppendLine("INGREDIENTS:");
            foreach (var ing in r.Ingredients.OrderBy(i => i.SortOrder))
            {
                var line = $"  • {ing.Quantity} {ing.Unit} {ing.Name}".Replace("  ", " ").Trim();
                if (!string.IsNullOrWhiteSpace(ing.Notes)) line += $" ({ing.Notes})";
                sb.AppendLine(line);
            }
            sb.AppendLine();
            sb.AppendLine("INSTRUCTIONS:");
            sb.AppendLine(r.Instructions);
            sb.AppendLine();
            sb.AppendLine(new string('-', 40));
        }

        return sb.ToString();
    }

    private static RecipeDto MapRecipe(Recipe r) => new()
    {
        Id = r.Id,
        Name = r.Name,
        Description = r.Description,
        Category = r.Category,
        BaseServings = r.BaseServings,
        PrepTime = r.PrepTime,
        CookTime = r.CookTime,
        Instructions = r.Instructions,
        UpdatedAt = r.UpdatedAt,
        Ingredients = r.Ingredients.OrderBy(i => i.SortOrder).Select(i => new RecipeIngredientDto
        {
            Id = i.Id,
            Name = i.Name,
            Quantity = i.Quantity,
            Unit = i.Unit,
            Notes = i.Notes
        }).ToList()
    };
}

public class RecipeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public int BaseServings { get; set; }
    public string? PrepTime { get; set; }
    public string? CookTime { get; set; }
    public string Instructions { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
    public List<RecipeIngredientDto> Ingredients { get; set; } = new();
}

public class RecipeIngredientDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public string? Notes { get; set; }
}

public class SaveRecipeRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public int BaseServings { get; set; } = 4;
    public string? PrepTime { get; set; }
    public string? CookTime { get; set; }
    public string Instructions { get; set; } = string.Empty;
    public List<SaveIngredientRequest> Ingredients { get; set; } = new();
}

public class SaveIngredientRequest
{
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public string? Notes { get; set; }
}

public class RecipeSettingsDto
{
    public string? GoogleDocId { get; set; }
    public bool HasServiceAccount { get; set; }
    public string? ServiceAccountEmail { get; set; }
    public bool AutoSync { get; set; }
    public DateTimeOffset? LastSyncAt { get; set; }
}

public class SaveRecipeSettingsRequest
{
    public string? GoogleDocId { get; set; }
    public bool AutoSync { get; set; }
}
