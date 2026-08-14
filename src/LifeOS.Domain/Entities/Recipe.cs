namespace LifeOS.Domain.Entities;

public class Recipe : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public int BaseServings { get; set; } = 4;
    public string? PrepTime { get; set; }
    public string? CookTime { get; set; }
    public string Instructions { get; set; } = string.Empty;

    public List<RecipeIngredient> Ingredients { get; set; } = new();
    public List<RecipeInstructionStep> Steps { get; set; } = new();
}
