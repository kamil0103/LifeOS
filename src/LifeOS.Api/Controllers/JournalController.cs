using LifeOS.Application.DTOs.Personal;
using System.Text;
using LifeOS.Application.Interfaces;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class JournalController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IPersonalRecordService _records;
    private readonly IGoogleDocsService _googleDocs;
    private readonly ILogger<JournalController> _logger;

    public JournalController(AppDbContext context, IPersonalRecordService records, IGoogleDocsService googleDocs, ILogger<JournalController> logger)
    {
        _context = context;
        _records = records;
        _googleDocs = googleDocs;
        _logger = logger;
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(sub!);
    }

    // ==================== ENTRIES ====================

    [HttpGet]
    public async Task<ActionResult<List<JournalEntryDto>>> GetEntries(CancellationToken ct) => await _records.JournalAsync(GetUserId(), ct);
    [HttpPost]
    public async Task<ActionResult<JournalEntryDto>> CreateEntry(CreateJournalEntryRequest request, CancellationToken ct)
    { var result = await _records.SaveJournalAsync(GetUserId(), null, request, ct); await MaybeAutoSync(GetUserId(), ct); return Ok(result); }
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<JournalEntryDto>> UpdateEntry(Guid id, UpdateJournalEntryRequest request, CancellationToken ct)
    { var result = await _records.SaveJournalAsync(GetUserId(), id, request, ct); if (result == null) return NotFound(); await MaybeAutoSync(GetUserId(), ct); return Ok(result); }
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteEntry(Guid id, CancellationToken ct)
    { if (!await _records.DeleteJournalAsync(GetUserId(), id, ct)) return NotFound(); await MaybeAutoSync(GetUserId(), ct); return NoContent(); }

    // ==================== SETTINGS ====================

    [HttpGet("settings")]
    public async Task<ActionResult<JournalSettingsDto>> GetSettings(CancellationToken ct)
    {
        var userId = GetUserId();
        var settings = await _context.JournalSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);

        return Ok(new JournalSettingsDto
        {
            GoogleDocId = settings?.GoogleDocId,
            HasServiceAccount = _googleDocs.IsConfigured,
            ServiceAccountEmail = _googleDocs.ServiceAccountEmail,
            AutoSync = settings?.AutoSync ?? false,
            LastSyncAt = settings?.LastSyncAt
        });
    }

    [HttpPut("settings")]
    public async Task<ActionResult<JournalSettingsDto>> SaveSettings(SaveJournalSettingsRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var settings = await _context.JournalSettings
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);

        if (settings == null)
        {
            settings = new JournalSettings { UserId = userId };
            _context.JournalSettings.Add(settings);
        }

        settings.GoogleDocId = request.GoogleDocId;
        settings.AutoSync = request.AutoSync;
        settings.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(ct);

        return Ok(new JournalSettingsDto
        {
            GoogleDocId = settings.GoogleDocId,
            HasServiceAccount = _googleDocs.IsConfigured,
            ServiceAccountEmail = _googleDocs.ServiceAccountEmail,
            AutoSync = settings.AutoSync,
            LastSyncAt = settings.LastSyncAt
        });
    }

    // ==================== SYNC ====================

    [HttpPost("sync/test")]
    public async Task<ActionResult<object>> TestSync(CancellationToken ct)
    {
        var userId = GetUserId();
        var settings = await _context.JournalSettings.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId, ct);
        if (string.IsNullOrWhiteSpace(settings?.GoogleDocId))
            return BadRequest(new ProblemDetails { Title = "Not configured", Detail = "Set your Google Doc ID first." });
        if (!_googleDocs.IsConfigured)
            return BadRequest(new ProblemDetails { Title = "Service account missing", Detail = "The server has no Google service account key configured." });

        try
        {
            var title = await _googleDocs.TestConnectionAsync(settings.GoogleDocId, ct);
            return Ok(new { success = true, documentTitle = title });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google Docs test connection failed");
            return BadRequest(new ProblemDetails { Title = "Connection failed", Detail = "Could not access the export document. Check its ID, sharing permissions or whether the document changed, then try again." });
        }
    }

    [HttpPost("sync")]
    public async Task<ActionResult<object>> Sync(CancellationToken ct)
    {
        var userId = GetUserId();
        var settings = await _context.JournalSettings.FirstOrDefaultAsync(s => s.UserId == userId, ct);
        if (string.IsNullOrWhiteSpace(settings?.GoogleDocId))
            return BadRequest(new ProblemDetails { Title = "Not configured", Detail = "Set your Google Doc ID first." });
        if (!_googleDocs.IsConfigured)
            return BadRequest(new ProblemDetails { Title = "Service account missing", Detail = "The server has no Google service account key configured." });

        var entries = await _records.JournalAsync(userId, ct);

        var content = BuildJournalDocument(entries);

        try
        {
            await _googleDocs.SyncContentAsync(settings.GoogleDocId, content, ct, "journal");
            settings.LastSyncAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(ct);
            return Ok(new { success = true, entriesSynced = entries.Count, syncedAt = settings.LastSyncAt });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google Docs sync failed");
            return BadRequest(new ProblemDetails { Title = "Sync failed", Detail = "Could not access the export document. Check its ID, sharing permissions or whether the document changed, then try again." });
        }
    }

    private async Task MaybeAutoSync(Guid userId, CancellationToken ct)
    {
        try
        {
            var settings = await _context.JournalSettings.FirstOrDefaultAsync(s => s.UserId == userId, ct);
            if (settings?.AutoSync != true || string.IsNullOrWhiteSpace(settings.GoogleDocId) || !_googleDocs.IsConfigured)
                return;

            var entries = await _records.JournalAsync(userId, ct);
            await _googleDocs.SyncContentAsync(settings.GoogleDocId, BuildJournalDocument(entries), ct, "journal");
            settings.LastSyncAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Auto-sync failed (non-fatal)");
        }
    }

    private static string BuildJournalDocument(List<JournalEntryDto> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("LifeOS Journal");
        sb.AppendLine($"Last synced: {DateTimeOffset.UtcNow:MMMM d, yyyy h:mm tt} UTC");
        sb.AppendLine();
        sb.AppendLine(new string('=', 50));

        foreach (var e in entries)
        {
            sb.AppendLine();
            sb.AppendLine($"{DateOnly.Parse(e.LocalDate):dddd, MMMM d, yyyy}");
            sb.AppendLine(e.Title);
            if (!string.IsNullOrWhiteSpace(e.Mood)) sb.AppendLine($"Mood: {e.Mood}");
            sb.AppendLine();
            sb.AppendLine(e.Content);
            sb.AppendLine();
            sb.AppendLine(new string('-', 40));
        }

        return sb.ToString();
    }

}
