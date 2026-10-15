namespace LifeOS.Application.DTOs.Personal;

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
    public List<RecipeStepDto> Steps { get; set; } = new();
}

public class RecipeStepDto
{
    public Guid Id { get; set; }
    public int StepNumber { get; set; }
    public string StepType { get; set; } = "prep";
    public string Text { get; set; } = string.Empty;
    public List<StepVariableDto> Variables { get; set; } = new();
}

public class StepVariableDto
{
    public string Name { get; set; } = string.Empty;      // cooking_time, temperature, rest_time, preheat, custom...
    public decimal Value { get; set; }
    public string? Unit { get; set; }                     // min, °F, °C, etc.
    public string ScalingMode { get; set; } = "none";     // none | linear | sqrt
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
    public Guid? RequestId { get; set; }
    public DateTimeOffset? ExpectedUpdatedAt { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public int BaseServings { get; set; } = 4;
    public string? PrepTime { get; set; }
    public string? CookTime { get; set; }
    public string Instructions { get; set; } = string.Empty;
    public List<SaveIngredientRequest> Ingredients { get; set; } = new();
    public List<SaveStepRequest> Steps { get; set; } = new();
}

public class SaveStepRequest
{
    public string StepType { get; set; } = "prep";
    public string Text { get; set; } = string.Empty;
    public List<StepVariableDto> Variables { get; set; } = new();
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
