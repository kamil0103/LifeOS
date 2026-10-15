using LifeOS.Application.DTOs.Personal;
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
    private readonly IPersonalRecordService _records;
    private readonly IGoogleDocsService _googleDocs;
    private readonly ILogger<RecipesController> _logger;

    public RecipesController(AppDbContext context, IPersonalRecordService records, IGoogleDocsService googleDocs, ILogger<RecipesController> logger)
    {
        _context = context;
        _records = records;
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
            .Include(r => r.Ingredients).Include(r => r.Steps)
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
            .Include(r => r.Ingredients).Include(r => r.Steps)
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);

        if (recipe == null) return NotFound();
        return Ok(MapRecipe(recipe));
    }

    [HttpPost]
    public async Task<ActionResult<RecipeDto>> CreateRecipe(SaveRecipeRequest request, CancellationToken ct)
    { var result = await _records.SaveRecipeAsync(GetUserId(), null, request, ct); await MaybeAutoSync(GetUserId(), ct); return Ok(result); }
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RecipeDto>> UpdateRecipe(Guid id, SaveRecipeRequest request, CancellationToken ct)
    { var result = await _records.SaveRecipeAsync(GetUserId(), id, request, ct); if (result == null) return NotFound(); await MaybeAutoSync(GetUserId(), ct); return Ok(result); }
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteRecipe(Guid id, CancellationToken ct)
    { if (!await _records.DeleteRecipeAsync(GetUserId(), id, ct)) return NotFound(); await MaybeAutoSync(GetUserId(), ct); return NoContent(); }

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
            return BadRequest(new ProblemDetails { Title = "Connection failed", Detail = "Could not access the export document. Check its ID, sharing permissions or whether the document changed, then try again." });
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
            .Include(r => r.Ingredients).Include(r => r.Steps)
            .Where(r => r.UserId == userId)
            .OrderBy(r => r.Name)
            .ToListAsync(ct);

        var content = BuildRecipeDocument(recipes);

        try
        {
            await _googleDocs.SyncContentAsync(settings.GoogleDocId, content, ct, "recipes");
            settings.LastSyncAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(ct);
            return Ok(new { success = true, recipesSynced = recipes.Count, syncedAt = settings.LastSyncAt });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google Docs sync failed");
            return BadRequest(new ProblemDetails { Title = "Sync failed", Detail = "Could not access the export document. Check its ID, sharing permissions or whether the document changed, then try again." });
        }
    }

    private async Task MaybeAutoSync(Guid userId, CancellationToken ct)
    {
        try
        {
            var settings = await _context.RecipeSettings.FirstOrDefaultAsync(s => s.UserId == userId, ct);
            if (settings?.AutoSync != true || string.IsNullOrWhiteSpace(settings.GoogleDocId) || !_googleDocs.IsConfigured)
                return;

            var recipes = await _context.Recipes.AsNoTracking().Include(r => r.Ingredients).Include(r => r.Steps)
                .Where(r => r.UserId == userId).OrderBy(r => r.Name).ToListAsync(ct);
            await _googleDocs.SyncContentAsync(settings.GoogleDocId, BuildRecipeDocument(recipes), ct, "recipes");
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
            if (r.Steps.Any())
            {
                sb.AppendLine("INSTRUCTIONS:");
                foreach (var s in r.Steps.OrderBy(s => s.StepNumber))
                {
                    var vars = DeserializeVariables(s.VariablesJson);
                    var varText = vars.Count > 0
                        ? "  [" + string.Join(", ", vars.Select(v => $"{v.Name}: {v.Value}{v.Unit}")) + "]"
                        : "";
                    sb.AppendLine($"  {s.StepNumber}. [{s.StepType.ToUpperInvariant()}] {s.Text}{varText}");
                }
            }
            else if (!string.IsNullOrWhiteSpace(r.Instructions))
            {
                sb.AppendLine("INSTRUCTIONS:");
                sb.AppendLine(r.Instructions);
            }
            sb.AppendLine();
            sb.AppendLine(new string('-', 40));
        }

        return sb.ToString();
    }

    private static string SerializeVariables(List<StepVariableDto>? variables)
    {
        if (variables == null || variables.Count == 0) return "[]";
        return System.Text.Json.JsonSerializer.Serialize(variables);
    }

    private static List<StepVariableDto> DeserializeVariables(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<StepVariableDto>>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }
        catch { return new(); }
    }

    private static RecipeDto MapRecipe(Recipe r) => LifeOS.Infrastructure.Personal.PersonalRecordService.MapRecipe(r);
}
