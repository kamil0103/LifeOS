namespace LifeOS.Application.DTOs.Jobs;

public class CompanyNoteDto
{
    public Guid Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public int? Rating { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class CreateCompanyNoteRequest
{
    public string CompanyName { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public int? Rating { get; set; }
}

public class UpdateCompanyNoteRequest
{
    public string CompanyName { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public int? Rating { get; set; }
}
