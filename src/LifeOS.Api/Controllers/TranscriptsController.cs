using System.Text.Json;
using LifeOS.Application.DTOs.Education;
using LifeOS.Application.Interfaces;
using LifeOS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TranscriptsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAiProvider _aiProvider;

    public TranscriptsController(AppDbContext context, IAiProvider aiProvider)
    {
        _context = context;
        _aiProvider = aiProvider;
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(sub!);
    }

    [HttpPost("extract")]
    public async Task<ActionResult<ExtractedTranscriptDto>> Extract([FromBody] ExtractTranscriptRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Length < 50)
            return BadRequest(new ProblemDetails { Title = "Text too short", Detail = "Paste at least 50 characters of transcript text." });

        var systemPrompt = "You are an academic transcript parser. Extract structured data from raw transcript text. Return JSON only.";
        var userPrompt = $"Extract from this transcript:\n\n{request.Text.Substring(0, Math.Min(8000, request.Text.Length))}\n\nReturn JSON:\n{{\n  \"institution\": {{\n    \"name\": \"<name>\",\n    \"type\": \"<university|community_college|other>\",\n    \"location\": \"<city, state>\"\n  }},\n  \"degree\": {{\n    \"name\": \"<degree name>\",\n    \"field\": \"<field of study>\",\n    \"type\": \"<bachelors|masters|associates|certificate|other>\",\n    \"gpa\": \"<gpa if found>\",\n    \"honors\": \"<honors if found>\"\n  }},\n  \"courses\": [\n    {{\n      \"code\": \"<course code>\",\n      \"name\": \"<course name>\",\n      \"grade\": \"<grade>\",\n      \"credits\": \"<credits>\",\n      \"term\": \"<term>\"\n    }}\n  ]\n}}";

        try
        {
            var jsonResponse = await _aiProvider.CompleteJsonAsync(systemPrompt, userPrompt, ct);
            var result = JsonSerializer.Deserialize<ExtractedTranscriptDto>(jsonResponse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return result ?? new ExtractedTranscriptDto();
        }
        catch (Exception ex)
        {
            return BadRequest(new ProblemDetails { Title = "Extraction failed", Detail = ex.Message });
        }
    }

    [HttpPost("save")]
    public async Task<ActionResult<object>> SaveExtracted([FromBody] SaveExtractedRequest request, CancellationToken ct)
    {
        var userId = GetUserId();

        // Create or find institution
        var institution = await _context.Institutions.FirstOrDefaultAsync(i => i.UserId == userId && i.Name == request.Institution.Name, ct);
        if (institution == null)
        {
            institution = new Domain.Entities.Institution
            {
                UserId = userId,
                Name = request.Institution.Name,
                InstitutionType = request.Institution.Type ?? "other",
                Location = request.Institution.Location
            };
            _context.Institutions.Add(institution);
            await _context.SaveChangesAsync(ct);
        }

        // Create degree
        var degree = new Domain.Entities.Degree
        {
            UserId = userId,
            InstitutionId = institution.Id,
            DegreeName = request.Degree.Name,
            Field = request.Degree.Field,
            DegreeType = request.Degree.Type ?? "other",
            Gpa = request.Degree.Gpa,
            Honors = request.Degree.Honors
        };
        _context.Degrees.Add(degree);
        await _context.SaveChangesAsync(ct);

        // Create courses
        foreach (var c in request.Courses)
        {
            _context.Courses.Add(new Domain.Entities.Course
            {
                UserId = userId,
                InstitutionId = institution.Id,
                DegreeId = degree.Id,
                Code = c.Code,
                Name = c.Name,
                Grade = c.Grade,
                Credits = decimal.TryParse(c.Credits, out var cred) ? cred : null,
                Term = c.Term,
                IsMajorRelated = true
            });
        }

        await _context.SaveChangesAsync(ct);

        return new { institutionId = institution.Id, degreeId = degree.Id, coursesAdded = request.Courses.Count };
    }
}

public class ExtractTranscriptRequest
{
    public string Text { get; set; } = string.Empty;
}

public class ExtractedTranscriptDto
{
    public ExtractedInstitutionDto Institution { get; set; } = new();
    public ExtractedDegreeDto Degree { get; set; } = new();
    public List<ExtractedCourseDto> Courses { get; set; } = new();
}

public class ExtractedInstitutionDto
{
    public string Name { get; set; } = string.Empty;
    public string? Type { get; set; }
    public string? Location { get; set; }
}

public class ExtractedDegreeDto
{
    public string Name { get; set; } = string.Empty;
    public string? Field { get; set; }
    public string? Type { get; set; }
    public string? Gpa { get; set; }
    public string? Honors { get; set; }
}

public class ExtractedCourseDto
{
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Grade { get; set; }
    public string? Credits { get; set; }
    public string? Term { get; set; }
}

public class SaveExtractedRequest
{
    public ExtractedInstitutionDto Institution { get; set; } = new();
    public ExtractedDegreeDto Degree { get; set; } = new();
    public List<ExtractedCourseDto> Courses { get; set; } = new();
}
