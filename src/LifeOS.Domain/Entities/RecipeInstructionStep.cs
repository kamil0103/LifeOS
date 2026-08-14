namespace LifeOS.Domain.Entities;

public class RecipeInstructionStep : BaseEntity
{
    public Guid RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;

    public int StepNumber { get; set; }
    public string StepType { get; set; } = "prep"; // prep, mix, cook, bake, fry, boil, simmer, rest, chill, serve, note, other
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// JSON array of step variables:
    /// [{ "name": "cooking_time", "value": 30, "unit": "min", "scalingMode": "sqrt" }, ...]
    /// scalingMode: none | linear | sqrt
    /// </summary>
    public string VariablesJson { get; set; } = "[]";
}
