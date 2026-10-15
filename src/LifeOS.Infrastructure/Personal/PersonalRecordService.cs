using System.Globalization;
using System.Text.Json;
using LifeOS.Application.DTOs.Personal;
using LifeOS.Application.Interfaces;
using LifeOS.Application.Services;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace LifeOS.Infrastructure.Personal;
public class PersonalRecordService(AppDbContext db, ILocalDayService days) : IPersonalRecordService
{
    private static void CheckVersion(DateTimeOffset? expected, DateTimeOffset actual)
    {
        if (expected.HasValue && expected.Value.UtcTicks / 10 != actual.UtcTicks / 10) throw new RecordConflictException("This record changed in another session. Reopen it before saving; your draft is kept.");
    }
    private static DateTimeOffset NextVersion(DateTimeOffset previous)
    {
        var now = DateTimeOffset.UtcNow.UtcTicks; now -= now % 10;
        var old = previous.UtcTicks - previous.UtcTicks % 10;
        return new DateTimeOffset(Math.Max(now, old + 10), TimeSpan.Zero);
    }
    private static JournalEntryDto Map(JournalEntry e, LocalDay day) => new() { Id = e.Id, Title = e.Title, Content = e.Content, EntryDate = e.EntryDate, LocalDate = (e.JournalDate ?? DateOnly.FromDateTime(e.EntryDate.UtcDateTime)).ToString("yyyy-MM-dd"), Mood = e.Mood, CreatedAt = e.CreatedAt, UpdatedAt = e.UpdatedAt };
    public async Task<List<JournalEntryDto>> JournalAsync(Guid userId, CancellationToken ct)
    {
        var day = await days.TodayAsync(userId, ct); var records = await db.JournalEntries.AsNoTracking().Where(e => e.UserId == userId).OrderByDescending(e => e.EntryDate).ToListAsync(ct); return records.Select(e => Map(e, day)).OrderByDescending(e => e.LocalDate).ThenByDescending(e => e.CreatedAt).ToList();
    }
    public async Task<JournalEntryDto?> SaveJournalAsync(Guid userId, Guid? id, CreateJournalEntryRequest r, CancellationToken ct)
    {
        var validation = new CreateJournalEntryRequestValidator().Validate(r); if (!validation.IsValid) throw new InvalidOperationException(string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));
        var day = await days.TodayAsync(userId, ct); DateTimeOffset? date = r.EntryDate?.ToUniversalTime();
        DateOnly? label = r.EntryDate.HasValue ? DateOnly.FromDateTime(r.EntryDate.Value.UtcDateTime) : null;
        if (!string.IsNullOrWhiteSpace(r.Date))
        {
            if (!DateOnly.TryParseExact(r.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local) || local.Year == 9999) throw new InvalidOperationException("Choose a valid journal date.");
            label = local;
            date = day.Offset((local.ToDateTime(TimeOnly.MinValue) - day.Date).Days).Start;
        }
        return await PersonalWriteLock.RunAsync<JournalEntryDto?>(db, userId, async () => {
            var entry = id.HasValue ? await db.JournalEntries.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, ct) : null;
            if (id.HasValue && entry == null) return null;
            if (!id.HasValue && r.RequestId.HasValue)
            {
                var prior = await db.JournalEntries.FirstOrDefaultAsync(e => e.Id == r.RequestId.Value, ct);
                if (prior != null) { if (prior.UserId != userId || prior.Title != r.Title.Trim() || prior.Content != r.Content || prior.Mood != r.Mood || (date.HasValue && prior.EntryDate != date)) throw new InvalidOperationException("This draft identifier was already saved with different content. Reopen the saved entry."); return Map(prior, day); }
            }
            if (entry == null) { entry = new() { Id = r.RequestId ?? Guid.NewGuid(), UserId = userId }; db.JournalEntries.Add(entry); }
            else CheckVersion(r.ExpectedUpdatedAt, entry.UpdatedAt);
            entry.Title = r.Title.Trim(); entry.Content = r.Content; entry.Mood = r.Mood; entry.EntryDate = date ?? (id.HasValue ? entry.EntryDate : DateTimeOffset.UtcNow); entry.JournalDate = label ?? (id.HasValue ? entry.JournalDate : DateOnly.FromDateTime(day.Date)); entry.UpdatedAt = NextVersion(entry.UpdatedAt);
            return Map(entry, day);
        }, ct);
    }
    public Task<bool> DeleteJournalAsync(Guid userId, Guid id, CancellationToken ct) => PersonalWriteLock.RunAsync(db, userId, async () => {
        var entry = await db.JournalEntries.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, ct); if (entry == null) return false; db.JournalEntries.Remove(entry); return true;
    }, ct);
    private static List<StepVariableDto> Variables(string? json) { try { return JsonSerializer.Deserialize<List<StepVariableDto>>(json ?? "[]", new JsonSerializerOptions { PropertyNameCaseInsensitive = true })?.Where(v => v != null).ToList() ?? []; } catch (JsonException) { return []; } }
    private static bool FixedTemperature(StepVariableDto v) => (v.Name ?? "").Contains("temp", StringComparison.OrdinalIgnoreCase) || string.Equals(v.Name, "preheat", StringComparison.OrdinalIgnoreCase) || v.Unit is "°F" or "°C";
    private static StepVariableDto Normalize(StepVariableDto v) => new() { Name = v.Name ?? "custom", Unit = v.Unit, Value = v.Value, ScalingMode = FixedTemperature(v) ? "none" : v.ScalingMode };
    public static RecipeDto MapRecipe(Recipe r) => new() { Id = r.Id, Name = r.Name, Description = r.Description, Category = r.Category, BaseServings = r.BaseServings, PrepTime = r.PrepTime, CookTime = r.CookTime, Instructions = r.Instructions, UpdatedAt = r.UpdatedAt,
        Ingredients = r.Ingredients.OrderBy(i => i.SortOrder).Select(i => new RecipeIngredientDto { Id = i.Id, Name = i.Name, Quantity = i.Quantity, Unit = i.Unit, Notes = i.Notes }).ToList(),
        Steps = r.Steps.OrderBy(s => s.StepNumber).Select(s => new RecipeStepDto { Id = s.Id, StepNumber = s.StepNumber, StepType = s.StepType, Text = s.Text, Variables = Variables(s.VariablesJson).Select(Normalize).ToList() }).ToList() };
    private static string Signature(SaveRecipeRequest r) => JsonSerializer.Serialize(new { Name = r.Name.Trim(), r.Description, r.Category, r.BaseServings, r.PrepTime, r.CookTime, r.Instructions,
        Ingredients = r.Ingredients.Select(i => new { Name = i.Name.Trim(), i.Quantity, i.Unit, i.Notes }), Steps = r.Steps.Select(s => new { s.StepType, s.Text, Variables = s.Variables.Select(Normalize) }) });
    private static SaveRecipeRequest ToRequest(Recipe r) => new() { Name = r.Name, Description = r.Description, Category = r.Category, BaseServings = r.BaseServings, PrepTime = r.PrepTime, CookTime = r.CookTime, Instructions = r.Instructions,
        Ingredients = r.Ingredients.OrderBy(i => i.SortOrder).Select(i => new SaveIngredientRequest { Name = i.Name, Quantity = i.Quantity, Unit = i.Unit, Notes = i.Notes }).ToList(),
        Steps = r.Steps.OrderBy(s => s.StepNumber).Select(s => new SaveStepRequest { StepType = s.StepType, Text = s.Text, Variables = Variables(s.VariablesJson) }).ToList() };
    public Task<RecipeDto?> SaveRecipeAsync(Guid userId, Guid? id, SaveRecipeRequest r, CancellationToken ct)
    {
        var validation = new SaveRecipeRequestValidator().Validate(r); if (!validation.IsValid) throw new InvalidOperationException(string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));
        return PersonalWriteLock.RunAsync<RecipeDto?>(db, userId, async () => {
            var recipe = id.HasValue ? await db.Recipes.Include(r => r.Ingredients).Include(r => r.Steps).FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct) : null;
            if (id.HasValue && recipe == null) return null;
            if (!id.HasValue && r.RequestId.HasValue)
            {
                var prior = await db.Recipes.Include(r => r.Ingredients).Include(r => r.Steps).FirstOrDefaultAsync(e => e.Id == r.RequestId.Value, ct);
                if (prior != null) { if (prior.UserId != userId || Signature(ToRequest(prior)) != Signature(r)) throw new InvalidOperationException("This draft was already saved with different content. Reopen the saved recipe."); return MapRecipe(prior); }
            }
            if (recipe == null) { recipe = new() { Id = r.RequestId ?? Guid.NewGuid(), UserId = userId }; db.Recipes.Add(recipe); }
            else { CheckVersion(r.ExpectedUpdatedAt, recipe.UpdatedAt); db.RecipeIngredients.RemoveRange(recipe.Ingredients); db.RecipeInstructionSteps.RemoveRange(recipe.Steps); recipe.Ingredients = []; recipe.Steps = []; }
            recipe.Name = r.Name.Trim(); recipe.Description = r.Description; recipe.Category = r.Category; recipe.BaseServings = r.BaseServings; recipe.PrepTime = r.PrepTime; recipe.CookTime = r.CookTime; recipe.Instructions = r.Instructions; recipe.UpdatedAt = NextVersion(recipe.UpdatedAt);
            foreach (var (i, index) in r.Ingredients.Select((value, index) => (value, index))) { var ingredient = new RecipeIngredient { RecipeId = recipe.Id, Name = i.Name.Trim(), Quantity = i.Quantity, Unit = i.Unit, Notes = i.Notes, SortOrder = index }; db.RecipeIngredients.Add(ingredient); }
            foreach (var (s, index) in r.Steps.Select((value, index) => (value, index))) { var step = new RecipeInstructionStep { RecipeId = recipe.Id, StepNumber = index + 1, StepType = s.StepType, Text = s.Text, VariablesJson = JsonSerializer.Serialize(s.Variables.Select(Normalize)) }; db.RecipeInstructionSteps.Add(step); }
            // Relationship fixup attaches the explicitly added rows to the tracked recipe.
            return MapRecipe(recipe);
        }, ct);
    }
    public Task<bool> DeleteRecipeAsync(Guid userId, Guid id, CancellationToken ct) => PersonalWriteLock.RunAsync(db, userId, async () => {
        var recipe = await db.Recipes.Include(r => r.Ingredients).Include(r => r.Steps).FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct); if (recipe == null) return false;
        db.RecipeIngredients.RemoveRange(recipe.Ingredients); db.RecipeInstructionSteps.RemoveRange(recipe.Steps); db.Recipes.Remove(recipe); return true;
    }, ct);
}
