using System.ComponentModel.DataAnnotations;
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
    [Required, StringLength(200)]
    public string CompanyName { get; set; } = string.Empty;
    [StringLength(20000)]
    public string? Notes { get; set; }
    [Range(1,5)]
    public int? Rating { get; set; }
}

public class UpdateCompanyNoteRequest
{
    [Required, StringLength(200)]
    public string CompanyName { get; set; } = string.Empty;
    [StringLength(20000)]
    public string? Notes { get; set; }
    [Range(1,5)]
    public int? Rating { get; set; }
}
