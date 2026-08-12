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

        // Step 2: Universal course extraction — chunked AI works with ANY transcript format.
        // Each chunk is small (one term / ~2000 chars) so responses never hit token limits,
        // and JsonRepair (in the providers) fixes any truncated JSON.
        var chunks = SplitIntoChunks(text);
        _logger.LogInformation("Split transcript into {Count} chunks for AI extraction", chunks.Count);

        var chunkTasks = chunks.Select(c => ExtractCoursesFromChunkAsync(c, ct)).ToList();
        var chunkResults = await Task.WhenAll(chunkTasks);

        var allCourses = new List<ExtractedCourseDto>();
        foreach (var result in chunkResults)
        {
            if (result != null) allCourses.AddRange(result);
        }
        _logger.LogInformation("AI extracted {Count} courses across {Chunks} chunks", allCourses.Count, chunks.Count);

        // Fallback: regex parser if AI returned nothing
        if (allCourses.Count == 0)
        {
            _logger.LogInformation("AI course extraction returned 0 courses; falling back to regex parser");
            allCourses = ParseCoursesFromText(text);
        }

        // Step 3: Dedup repeats — same normalized code + name, keep highest grade
        metaResult.Courses = DedupCourses(allCourses);

        return metaResult;
    }

    /// <summary>
    /// Split transcript text into chunks at term boundaries. Falls back to ~2000-char
    /// chunks split at whitespace when no term headers are found.
    /// </summary>
    private static List<string> SplitIntoChunks(string text)
    {
        var termRegex = new Regex(@"(Fall|Spring|Summer|Winter)\s*((?:Semester|Term|Session|Intersession|Quarter)\s*)?(19|20)\d{2}", RegexOptions.IgnoreCase);
        var matches = termRegex.Matches(text);

        var chunks = new List<string>();
        if (matches.Count >= 2)
        {
            for (int i = 0; i < matches.Count; i++)
            {
                var start = matches[i].Index;
                var end = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;
                var chunk = text[start..end].Trim();
                if (chunk.Length > 30) chunks.Add(chunk);
            }
        }
        else
        {
            // No term headers — fixed-size chunks at whitespace boundaries
            const int size = 2000;
            for (int i = 0; i < text.Length; i += size)
            {
                var end = Math.Min(i + size, text.Length);
                if (end < text.Length)
                {
                    var lastSpace = text.LastIndexOf(' ', end, Math.Min(size - 1, end - i));
                    if (lastSpace > i) end = lastSpace;
                }
                var chunk = text[i..end].Trim();
                if (chunk.Length > 30) chunks.Add(chunk);
                if (end <= i) break;
                i = end - 1; // for-loop increments
            }
        }
        return chunks;
    }

    private async Task<List<ExtractedCourseDto>?> ExtractCoursesFromChunkAsync(string chunk, CancellationToken ct)
    {
        var systemPrompt = "You are an academic transcript parser. You extract every course row from transcript text regardless of layout or spacing. You never invent courses.";
        var userPrompt = $@"Extract ALL courses from this transcript section. The text may come from any school's transcript — columns may be concatenated or space-separated, grades may appear before or after units, there may be extra columns (GE codes, footnotes, GPA) — parse intelligently.

TRANSCRIPT SECTION:
---
{chunk}
---

Return ONLY a JSON array (no markdown, no commentary):
[{{""code"":""<course code like 'COMS 120' or 'CSE2130'>"",""name"":""<course title>"",""grade"":""<letter grade like A, B+, C-, P, W, F>"",""credits"":""<units like 3.0>"",""term"":""<term header from this section, e.g. 'Fall Semester 2021'>""}}]

Rules:
- Include EVERY course row, even repeated or withdrawn courses.
- credits = the course's units (attempted value).
- term = copy the term header text exactly as shown in this section (e.g. 'Summer Term 2021', 'Fall 2023').
- Ignore totals/summary/GPA lines, headers, and footnotes.
- If this section contains no course rows, return [].";

        try
        {
            var json = await _aiProvider.CompleteJsonAsync(systemPrompt, userPrompt, ct);
            var cleaned = CleanupJsonResponse(json);

            // Response may be a bare array — wrap if needed for repair tolerance
            var courses = JsonSerializer.Deserialize<List<ExtractedCourseDto>>(cleaned, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return courses?.Where(c => !string.IsNullOrWhiteSpace(c.Name)).ToList() ?? new List<ExtractedCourseDto>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI chunk extraction failed (chunk length {Length}), skipping chunk", chunk.Length);
            return null;
        }
    }

    private static List<ExtractedCourseDto> DedupCourses(List<ExtractedCourseDto> courses)
    {
        var best = new Dictionary<string, ExtractedCourseDto>();
        var order = new List<string>();
        foreach (var c in courses)
        {
            var key = (c.Code ?? "").Replace("-", "").Replace(" ", "").ToUpperInvariant()
                + "|" + c.Name.Replace(" ", "").ToUpperInvariant();

            if (!best.TryGetValue(key, out var existing))
            {
                best[key] = c;
                order.Add(key);
            }
            else if (GradeRank.GetValueOrDefault(c.Grade ?? "", 0) > GradeRank.GetValueOrDefault(existing.Grade ?? "", 0))
            {
                best[key] = c;
            }
        }
        return order.Select(k => best[k]).ToList();
    }

    private static readonly Dictionary<string, double> GradeValues = new()
    {
        ["A+"] = 4.0, ["A"] = 4.0, ["A-"] = 3.7,
        ["B+"] = 3.3, ["B"] = 3.0, ["B-"] = 2.7,
        ["C+"] = 2.3, ["C"] = 2.0, ["C-"] = 1.7,
        ["D+"] = 1.3, ["D"] = 1.0, ["D-"] = 0.7,
        ["F"] = 0.0,
    };

    private static readonly Dictionary<string, int> GradeRank = new()
    {
        ["A+"] = 13, ["A"] = 12, ["A-"] = 11, ["B+"] = 10, ["B"] = 9, ["B-"] = 8,
        ["C+"] = 7, ["C"] = 6, ["C-"] = 5, ["D+"] = 4, ["D"] = 3, ["D-"] = 2,
        ["P"] = 6, ["CR"] = 6, ["F"] = 1, ["W"] = 0, ["NP"] = 0, ["NC"] = 0
    };

    private const string SuffixLetters = "ABCDHLNRSTWXYZ";

    private sealed class ParsedCourse
    {
        public string Code = "";
        public string Name = "";
        public string Grade = "";
        public string Credits = "";
        public string? Term;
        public int Index;
    }

    private static List<ExtractedCourseDto> ParseCoursesFromText(string rawText)
    {
        // Truncate legend/footer sections (grading keys, accreditation, FERPA notes)
        var cutMarkers = new[] { "End of Transcript", "ACCREDITATION", "GRADING AND ACADEMIC RECORD", "Grading System", "THIS TRANSCRIPT IS", "Official Transcript of Academic Record" };
        var text = rawText;
        foreach (var marker in cutMarkers)
        {
            var idx = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx > 0) text = text[..idx];
        }

        // Unwrap repeat/withdrawal credit markers: "[5.00]" -> "", "(4.00)" -> "4.00", "5.00R" -> "5.00"
        text = Regex.Replace(text, @"\[\d+\.\d+\]", "");
        text = Regex.Replace(text, @"\((\d+\.\d+)\)", "$1");
        text = Regex.Replace(text, @"(\d\.\d{2})R(?=[A-Z]{2,})", "$1");

        // Term boundaries (middle word optional: "Fall 2023" and "FallSemester2021" both work)
        var termRegex = new Regex(@"(Fall|Spring|Summer|Winter)\s*((?:Semester|Term|Session|Intersession)\s*)?(\d{4})", RegexOptions.IgnoreCase);
        var boundaries = new List<(int Index, string Term)>();
        foreach (Match tm in termRegex.Matches(text))
        {
            var middle = tm.Groups[2].Value.Trim();
            var termName = string.IsNullOrEmpty(middle)
                ? $"{tm.Groups[1].Value} {tm.Groups[3].Value}"
                : $"{tm.Groups[1].Value} {middle} {tm.Groups[3].Value}";
            boundaries.Add((tm.Index, termName));
        }

        // Format 1 (concatenated: El Camino / CSUSB style):
        // Code + Name (no commas) + Grade (A-D, F, W, P — NOT E) + 1-3 decimal numbers + boundary
        // Boundary: end, separator, CamelCase word, or all-caps word (SEMESTERTOTAL, CUMULATIVE, WINC...)
        var coursePattern = @"(?<![A-Z])([A-Z]{2,}-?\d+)([A-Z][A-Za-z0-9 .&/+'():;-]*?)(A[+-]?|B[+-]?|C[+-]?|D[+-]?|F|W|P)(\d+\.\d+(?:\d+\.\d+){0,2})(?=$|[^A-Za-z0-9.]|[A-Z][a-z]|[A-Z]{2,})";

        // Format 2 (space-separated, units-first: CSUF style): "ACCT 301A Intermediate Accounting  3.0  C  6.0"
        var spacedPattern = @"(?<![A-Z])([A-Z]{2,5})\s+(\d{3,4}[A-Z]?)\s+([A-Z][A-Za-z0-9 .&/+'():;-]*?)\s+\(?(\d+\.\d)\)?\s+(A[+-]?|B[+-]?|C[+-]?|D[+-]?|F|CR|NC|NP|IP|WU|SP|RP|RD|AU|W|P|I)\s+(\d+\.\d)";

        // Format 3 (space-separated, grade-first: unofficial El Camino self-service style):
        // "COMS 120 Argumentation and Debate  A  3.0  3.0  12.0" (grade BEFORE units)
        var gradeFirstPattern = @"(?<![A-Z])([A-Z]{2,5})\s+(\d{1,4}[A-Z]?)\s+([A-Z][A-Za-z0-9 .&/+'():;-]*?)\s+(A[+-]?|B[+-]?|C[+-]?|D[+-]?|F|CR|NC|NP|IP|WU|SP|RP|RD|AU|W|P|I)\s+(\d+\.\d)\s+(\d+\.\d)\s+(\d+\.\d)";

        // Collect raw matches from all three formats, ordered by position
        var rawMatches = new List<(int Index, string Code, string Name, string Grade, string Numbers, string? Units)>();
        foreach (Match m in Regex.Matches(text, coursePattern))
            rawMatches.Add((m.Index, m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value, null));
        foreach (Match m in Regex.Matches(text, spacedPattern))
            rawMatches.Add((m.Index, $"{m.Groups[1].Value} {m.Groups[2].Value}", m.Groups[3].Value, m.Groups[5].Value, m.Groups[6].Value, m.Groups[4].Value));
        foreach (Match m in Regex.Matches(text, gradeFirstPattern))
            rawMatches.Add((m.Index, $"{m.Groups[1].Value} {m.Groups[2].Value}", m.Groups[3].Value, m.Groups[4].Value, m.Groups[7].Value, m.Groups[5].Value));
        rawMatches = rawMatches.OrderBy(r => r.Index).ToList();

        var parsed = new List<ParsedCourse>();
        foreach (var m in rawMatches)
        {
            var code = m.Code;
            var name = m.Name.Trim();
            var grade = m.Grade;
            var numbers = m.Numbers;

            // Strip trailing credit numbers from name (CSUSB format: "NAME3.0003.000")
            name = Regex.Replace(name, @"\d+\.\d+(?:\d+\.\d+)*$", "").Trim();
            if (name.Length < 2) continue;
            if (name.Contains("TOTAL", StringComparison.OrdinalIgnoreCase)) continue;
            if (name.Contains("GPA", StringComparison.OrdinalIgnoreCase)) continue;
            if (name.Contains("STANDING", StringComparison.OrdinalIgnoreCase)) continue;
            if (name.Contains("CUMULATIVE", StringComparison.OrdinalIgnoreCase)) continue;

            // Rule 1: concatenated mixed-case names like "AGeneralPhysics" = code letter + real name
            // (Cap + Cap + lowercase signature: "AGeneral", "STrigonometry", "LGENERAL" no — all-caps handled in pass 2)
            if (name.Length >= 3 && char.IsUpper(name[0]) && char.IsUpper(name[1]) && char.IsLower(name[2]) && SuffixLetters.Contains(name[0]))
            {
                code += name[0];
                name = name[1..];
            }

            // Determine term from nearest preceding boundary
            string? term = null;
            for (int i = boundaries.Count - 1; i >= 0; i--)
            {
                if (boundaries[i].Index < m.Index) { term = boundaries[i].Term; break; }
            }

            // Credits: spaced format provides units directly; concatenated derives from quality points / grade value
            if (m.Units != null)
            {
                parsed.Add(new ParsedCourse { Code = code, Name = name, Grade = grade, Credits = m.Units, Term = term, Index = m.Index });
                continue;
            }

            var lastNumberMatch = Regex.Match(numbers, @"(\d+\.\d+)$");
            if (!lastNumberMatch.Success) continue;
            if (!double.TryParse(lastNumberMatch.Groups[1].Value, out var points)) continue;

            string credits;
            if (GradeValues.TryGetValue(grade, out var gradeVal) && gradeVal > 0)
            {
                var cred = Math.Round(points / gradeVal * 2) / 2;
                credits = $"{cred:F2}";
            }
            else
            {
                credits = points.ToString("F2");
            }

            parsed.Add(new ParsedCourse { Code = code, Name = name, Grade = grade, Credits = credits, Term = term, Index = m.Index });
        }

        // Pass 2 (group-based suffix for ALL-CAPS names): if a base-code group has multiple
        // DISTINCT names, the leading letter is a code suffix/section (e.g., PHYS2500 + PHYS2500L)
        foreach (var g in parsed.GroupBy(c => c.Code.Replace("-", "").ToUpperInvariant()).ToList())
        {
            if (g.Select(c => c.Name).Distinct().Count() < 2) continue;
            foreach (var c in g)
            {
                if (c.Name.Length >= 4 && SuffixLetters.Contains(c.Name[0]) && char.IsUpper(c.Name[1]))
                {
                    c.Code += c.Name[0];
                    c.Name = c.Name[1..];
                }
            }
        }

        // Dedup repeats: same normalized code + name, keep highest grade (C retake beats D, etc.)
        var best = new Dictionary<string, ParsedCourse>();
        foreach (var c in parsed)
        {
            var key = c.Code.Replace("-", "").Replace(" ", "").ToUpperInvariant() + "|" + c.Name.Replace(" ", "").ToUpperInvariant();
            if (!best.TryGetValue(key, out var existing))
                best[key] = c;
            else if (GradeRank.GetValueOrDefault(c.Grade, 0) > GradeRank.GetValueOrDefault(existing.Grade, 0))
                best[key] = c;
        }

        return best.Values.OrderBy(c => c.Index).Select(c => new ExtractedCourseDto
        {
            Code = c.Code,
            Name = c.Name,
            Grade = c.Grade,
            Credits = c.Credits,
            Term = c.Term
        }).ToList();
    }

    private static string CleanupJsonResponse(string response)
    {
        var cleaned = response.Trim();

        // Remove markdown code blocks
        if (cleaned.StartsWith("```json")) cleaned = cleaned[7..];
        else if (cleaned.StartsWith("```")) cleaned = cleaned[3..];
        if (cleaned.EndsWith("```")) cleaned = cleaned[..^3];

        cleaned = cleaned.Trim();

        // Extract the JSON payload — handle both objects {...} and arrays [...]
        var firstBrace = cleaned.IndexOf('{');
        var firstBracket = cleaned.IndexOf('[');
        int start;
        if (firstBrace < 0) start = firstBracket;
        else if (firstBracket < 0) start = firstBrace;
        else start = Math.Min(firstBrace, firstBracket);

        if (start >= 0)
        {
            var openChar = cleaned[start];
            var closeChar = openChar == '{' ? '}' : ']';
            var end = cleaned.LastIndexOf(closeChar);
            if (end > start)
            {
                cleaned = cleaned[start..(end + 1)];
            }
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

        Guid? degreeId = null;

        // Create degree only if provided (community colleges may not have a degree listed)
        if (request.Degree != null && !string.IsNullOrWhiteSpace(request.Degree.Name))
        {
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
            degreeId = degree.Id;
        }

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
                    DegreeId = degreeId,
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

        return new { institutionId = institution.Id, degreeId = degreeId, coursesAdded = request.Courses?.Count ?? 0 };
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
