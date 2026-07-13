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
                _logger.LogInformation("Extracted text sample:\n{Sample}", extracted.Substring(0, Math.Min(1000, extracted.Length)));
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
        var truncatedText = text.Length > 8000 ? text.Substring(0, 8000) : text;
        
        // Step 1: AI extracts institution and degree (small JSON, always fits in token limit)
        var metaSystemPrompt = "You are an academic transcript parser. Extract only the institution and degree metadata.";
        var metaUserPrompt = $"From this transcript text, extract the institution name, type, location, degree name, field, GPA, and honors. Return compact JSON:\n\nTRANSCRIPT TEXT:\n---\n{truncatedText}\n---\n\nReturn ONLY this JSON (use empty strings if not found):\n{{\"institution\":{{\"name\":\"...\",\"type\":\"university|community_college|other\",\"location\":\"...\"}},\"degree\":{{\"name\":\"...\",\"field\":\"...\",\"type\":\"bachelors|masters|associates|certificate|other\",\"gpa\":\"...\",\"honors\":\"...\"}}}}";

        ExtractedTranscriptDto? metaResult = null;
        try
        {
            var metaJson = await _aiProvider.CompleteJsonAsync(metaSystemPrompt, metaUserPrompt, ct);
            metaJson = CleanupJsonResponse(metaJson);
            _logger.LogInformation("Meta AI response length: {Length}", metaJson.Length);
            metaResult = JsonSerializer.Deserialize<ExtractedTranscriptDto>(metaJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI metadata extraction failed, will use local parsing only");
        }

        metaResult ??= new ExtractedTranscriptDto();

        // Step 2: Parse courses locally with regex (reliable, no token limits)
        var courses = ParseCoursesFromText(text);
        metaResult.Courses = courses;

        return metaResult;
    }

    private static readonly Dictionary<string, double> GradeValues = new()
    {
        ["A+"] = 4.0, ["A"] = 4.0, ["A-"] = 3.7,
        ["B+"] = 3.3, ["B"] = 3.0, ["B-"] = 2.7,
        ["C+"] = 2.3, ["C"] = 2.0, ["C-"] = 1.7,
        ["D+"] = 1.3, ["D"] = 1.0, ["D-"] = 0.7,
        ["F"] = 0.0,
    };

    private static List<ExtractedCourseDto> ParseCoursesFromText(string text)
    {
        var courses = new List<ExtractedCourseDto>();
        
        // Detect term headers in the full text to assign terms to courses
        var termMatches = Regex.Matches(text, @"(Fall|Spring|Summer|Winter)\s*(Semester|Term|Session|Intersession)\s*(\d{4})", RegexOptions.IgnoreCase);
        var termBoundaries = new List<(int Index, string Term)>();
        foreach (Match tm in termMatches)
        {
            termBoundaries.Add((tm.Index, $"{tm.Groups[1].Value} {tm.Groups[2].Value} {tm.Groups[3].Value}"));
        }
        
        // Course pattern for concatenated transcript text
        // Code + Name(starts with uppercase) + Grade + Numbers, stopping before next code/term/end
        // Common suffixes: S=Support, A/B/C/D=sections, H=Honors, L=Lab, N=Night, R=Recitation, W=Workshop, X/Y/Z=other
        var suffixPattern = @"(?:[SABCDHLNRWXYZ](?=[A-Z]))?";
        var coursePattern = $@"(?<![A-Z])([A-Z]{{2,}}\d+{suffixPattern})([A-Z].+?)([A-FWP][+-]?)\s*([\d.]+(?:[\d.]+)*?)\s*(?=(?:[A-Z]{{2,}}\d+)|SEMESTER|CUMULATIVE|TERM|Dean's|In Good|\-{{5,}}|$)";
        var matches = Regex.Matches(text, coursePattern);
        
        foreach (Match m in matches)
        {
            var code = m.Groups[1].Value;
            var name = m.Groups[2].Value.Trim();
            var grade = m.Groups[3].Value;
            var numbers = m.Groups[4].Value.Trim();
            
            // Strip trailing credit numbers from name (CSUSB format: "NAME3.0003.000")
            name = Regex.Replace(name, @"\d+\.\d+(?:\d+\.\d+)*$", "").Trim();
            
            // Filter out non-course lines
            if (name.Contains("TOTAL", StringComparison.OrdinalIgnoreCase)) continue;
            if (name.Contains("GPA", StringComparison.OrdinalIgnoreCase)) continue;
            if (name.Contains("STANDING", StringComparison.OrdinalIgnoreCase)) continue;
            if (name.Contains("CUMULATIVE", StringComparison.OrdinalIgnoreCase)) continue;
            if (name.Length < 2) continue;
            
            // Determine term by finding the nearest term boundary before this match
            string? term = null;
            for (int i = termBoundaries.Count - 1; i >= 0; i--)
            {
                if (termBoundaries[i].Index < m.Index)
                {
                    term = termBoundaries[i].Term;
                    break;
                }
            }
            
            // Calculate credits from last number (points) and grade
            var lastNumberMatch = Regex.Match(numbers, @"(\d+\.\d+)$");
            if (!lastNumberMatch.Success) continue;
            
            var pointsStr = lastNumberMatch.Groups[1].Value;
            if (!double.TryParse(pointsStr, out var points)) continue;
            
            string credits;
            if (GradeValues.TryGetValue(grade, out var gradeVal) && gradeVal > 0)
            {
                var cred = points / gradeVal;
                cred = Math.Round(cred * 2) / 2;
                credits = $"{cred:F2}";
            }
            else
            {
                credits = points.ToString("F2");
            }
            
            courses.Add(new ExtractedCourseDto
            {
                Code = code,
                Name = name,
                Grade = grade,
                Credits = credits,
                Term = term
            });
        }
        
        return courses;
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
