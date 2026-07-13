using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LifeOS.Application.DTOs.Education;
using LifeOS.Application.Interfaces;
using LifeOS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace LifeOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TranscriptsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAiProvider _aiProvider;
    private readonly ILogger<TranscriptsController> _logger;

    public TranscriptsController(AppDbContext context, IAiProvider aiProvider, ILogger<TranscriptsController> logger)
    {
        _context = context;
        _aiProvider = aiProvider;
        _logger = logger;
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

        var result = await ExtractFromText(request.Text, ct);
        return result ?? new ExtractedTranscriptDto();
    }

    [HttpPost("extract-file")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10MB max
    public async Task<ActionResult<ExtractedTranscriptDto>> ExtractFile(IFormFile file, CancellationToken ct)
    {
        var userId = GetUserId();
        
        if (file == null || file.Length == 0)
            return BadRequest(new ProblemDetails { Title = "No file", Detail = "Please upload a file." });

        if (file.Length > 5 * 1024 * 1024)
            return BadRequest(new ProblemDetails { Title = "File too large", Detail = "Max file size is 5MB." });

        // Supported types
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowed = new[] { ".txt", ".pdf", ".doc", ".docx", ".rtf", ".csv", ".tsv", ".json", ".xml", ".html", ".htm" };
        if (!allowed.Contains(ext))
            return BadRequest(new ProblemDetails { Title = "Unsupported file", Detail = $"Allowed: {string.Join(", ", allowed)}" });

        // Read file content
        string text;
        if (ext == ".pdf")
        {
            text = ExtractTextFromPdf(file);
        }
        else
        {
            using (var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8))
            {
                text = await reader.ReadToEndAsync(ct);
            }
        }

        if (string.IsNullOrWhiteSpace(text) || text.Length < 50)
        {
            if (ext == ".pdf")
                return BadRequest(new ProblemDetails { 
                    Title = "PDF parsing failed", 
                    Detail = "Could not extract readable text from this PDF. The PDF may be scanned (image-based) or password-protected. Try converting it to text first, or paste the text manually." 
                });
            return BadRequest(new ProblemDetails { Title = "Text too short", Detail = "File contained less than 50 characters of readable text." });
        }

        _logger.LogInformation("Extracted {Length} characters from uploaded file", text.Length);
        _logger.LogDebug("First 1000 chars of extracted text: {Sample}", text.Substring(0, Math.Min(1000, text.Length)));

        var result = await ExtractFromText(text, ct);
        
        if (result == null)
            return BadRequest(new ProblemDetails { Title = "Extraction failed", Detail = "AI could not parse the transcript. The file may not contain transcript data, or the format is not recognized." });

        // Check if anything was actually extracted
        if (string.IsNullOrWhiteSpace(result.Institution?.Name) && 
            string.IsNullOrWhiteSpace(result.Degree?.Name) && 
            (result.Courses == null || result.Courses.Count == 0))
        {
            return Ok(new ExtractedTranscriptDto 
            { 
                Institution = new ExtractedInstitutionDto { Name = "" },
                Degree = new ExtractedDegreeDto { Name = "" },
                Courses = new List<ExtractedCourseDto>()
            });
        }

        return result;
    }

    private string ExtractTextFromPdf(IFormFile file)
    {
        try
        {
            using var ms = new MemoryStream();
            file.CopyTo(ms);
            ms.Position = 0;
            
            // Use PdfPig for proper PDF text extraction
            string extracted;
            try
            {
                using var document = PdfDocument.Open(ms);
                var pages = document.GetPages();
                var textBuilder = new StringBuilder();
                foreach (var page in pages)
                {
                    var pageText = page.Text;
                    if (!string.IsNullOrWhiteSpace(pageText))
                    {
                        textBuilder.AppendLine(pageText);
                    }
                }
                extracted = textBuilder.ToString();
            }
            catch (Exception pdfEx)
            {
                _logger.LogWarning(pdfEx, "PdfPig failed for {FileName}, falling back to regex extraction", file.FileName);
                // Fallback to simple regex extraction
                extracted = FallbackPdfExtraction(ms);
            }

            _logger.LogInformation("PDF extraction: got {Length} readable characters from {FileName}", extracted.Length, file.FileName);
            if (!string.IsNullOrEmpty(extracted))
            {
                _logger.LogDebug("Extracted text sample: {Sample}", extracted.Substring(0, Math.Min(500, extracted.Length)));
            }
            
            return extracted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDF text extraction failed for {FileName}", file.FileName);
            return string.Empty;
        }
    }

    private string FallbackPdfExtraction(MemoryStream ms)
    {
        try
        {
            ms.Position = 0;
            var bytes = ms.ToArray();
            var content = Encoding.UTF8.GetString(bytes);
            var textBuilder = new StringBuilder();
            
            // Method 1: Extract text between BT and ET markers (PDF text objects)
            var btMatches = Regex.Matches(content, @"BT\s*(.*?)\s*ET", RegexOptions.Singleline);
            foreach (Match btMatch in btMatches)
            {
                var textInside = btMatch.Groups[1].Value;
                var stringMatches = Regex.Matches(textInside, @"\(([^)]{2,300})\)");
                foreach (Match sm in stringMatches)
                {
                    var txt = sm.Groups[1].Value;
                    if (IsReadableText(txt))
                    {
                        textBuilder.Append(txt).Append(' ');
                    }
                }
            }

            var extracted = textBuilder.ToString();
            
            // Method 2: Try finding all parenthesized strings
            if (extracted.Length < 200)
            {
                textBuilder.Clear();
                var allMatches = Regex.Matches(content, @"\(([^)]{3,300})\)");
                foreach (Match match in allMatches)
                {
                    var txt = match.Groups[1].Value;
                    if (IsReadableText(txt))
                    {
                        textBuilder.Append(txt).Append(' ');
                    }
                }
                extracted = textBuilder.ToString();
            }

            return extracted;
        }
        catch
        {
            return string.Empty;
        }
    }

    private bool IsReadableText(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 3) return false;
        if (!Regex.IsMatch(text, @"[a-zA-Z]{2,}")) return false;
        
        var lower = text.ToLowerInvariant();
        if (lower.Contains("obj") && lower.Contains("endobj")) return false;
        if (text.StartsWith("/")) return false;
        if (text.StartsWith("<<") || text.StartsWith(">>") || text.StartsWith("[")) return false;
        if (Regex.IsMatch(text, @"^\d+ \d+ \d+")) return false;
        if (text.Contains("stream") || text.Contains("endstream")) return false;
        if (text.Contains("xref") || text.Contains("trailer")) return false;
        
        var letters = text.Count(char.IsLetter);
        var ratio = (double)letters / text.Length;
        if (ratio < 0.5 && text.Length > 20) return false;
        
        return true;
    }

    private async Task<ExtractedTranscriptDto?> ExtractFromText(string text, CancellationToken ct)
    {
        var systemPrompt = "You are an academic transcript parser. Extract structured data from raw transcript text.";
        var userPrompt = $"Extract structured data from this academic transcript text. The text may have formatting issues, extra spaces, or PDF artifacts - parse it intelligently.\n\nTRANSCRIPT TEXT:\n---\n{text.Substring(0, Math.Min(8000, text.Length))}\n---\n\nReturn ONLY valid JSON with this exact structure. If information is missing, use empty strings or empty arrays - NEVER return null for required fields. Make sure all string values are properly escaped and the JSON is complete:\n{{\n  \"institution\": {{\n    \"name\": \"<institution name>\",\n    \"type\": \"<university|community_college|other>\",\n    \"location\": \"<city, state or empty>\"\n  }},\n  \"degree\": {{\n    \"name\": \"<full degree name like Bachelor of Science in Computer Science>\",\n    \"field\": \"<field of study>\",\n    \"type\": \"<bachelors|masters|associates|certificate|other>\",\n    \"gpa\": \"<gpa if found>\",\n    \"honors\": \"<honors if found>\"\n  }},\n  \"courses\": [\n    {{\n      \"code\": \"<course code like CS 101>\",\n      \"name\": \"<course name>\",\n      \"grade\": \"<grade like A, B+, etc>\",\n      \"credits\": \"<credits like 3.00>\",\n      \"term\": \"<term like Fall 2023>\"\n    }}\n  ]\n}}\n\nLook for patterns like:\n- Institution names (often at top: 'University of X', 'College of Y')\n- Degrees (look for 'Bachelor', 'Master', 'Associate', 'Certificate')\n- Fields (look for 'in' before subject: 'Bachelor of Science in Computer Science')\n- GPA values (look for 'GPA:', 'Grade Point Average')\n- Course lines with codes, names, grades, credits\n\nIf this is clearly NOT a transcript, return empty strings and empty courses array.";


        try
        {
            var jsonResponse = await _aiProvider.CompleteJsonAsync(systemPrompt, userPrompt, ct);
            
            _logger.LogInformation("AI raw response length: {Length}", jsonResponse.Length);
            _logger.LogDebug("AI raw response: {Response}", jsonResponse.Substring(0, Math.Min(500, jsonResponse.Length)));
            
            // Aggressive JSON cleanup
            var cleaned = CleanupJsonResponse(jsonResponse);
            
            var result = JsonSerializer.Deserialize<ExtractedTranscriptDto>(cleaned, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transcript extraction failed");
            return null;
        }
    }

    private static string CleanupJsonResponse(string response)
    {
        var cleaned = response.Trim();
        
        // Remove markdown code blocks
        if (cleaned.StartsWith("```json")) cleaned = cleaned[7..];
        else if (cleaned.StartsWith("```")) cleaned = cleaned[3..];
        if (cleaned.EndsWith("```")) cleaned = cleaned[..^3];
        
        cleaned = cleaned.Trim();
        
        // Extract just the JSON object - find first { and last }
        var firstBrace = cleaned.IndexOf('{');
        var lastBrace = cleaned.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            cleaned = cleaned[firstBrace..(lastBrace + 1)];
        }
        
        return cleaned;
    }

    [HttpPost("save")]
    public async Task<ActionResult<object>> SaveExtracted([FromBody] SaveExtractedRequest request, CancellationToken ct)
    {
        var userId = GetUserId();

        // Validate request
        if (request.Institution == null || string.IsNullOrWhiteSpace(request.Institution.Name))
            return BadRequest(new ProblemDetails { Title = "Missing institution", Detail = "Institution name is required." });

        if (request.Degree == null || string.IsNullOrWhiteSpace(request.Degree.Name))
            return BadRequest(new ProblemDetails { Title = "Missing degree", Detail = "Degree name is required." });

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
        if (request.Courses != null)
        {
            foreach (var c in request.Courses)
            {
                if (string.IsNullOrWhiteSpace(c.Name)) continue;
                
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
        }

        return new { institutionId = institution.Id, degreeId = degree.Id, coursesAdded = request.Courses?.Count ?? 0 };
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
