using LifeOS.Application.DTOs.Jobs;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/job-applications")]
[Authorize]
public class ApplicationsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly LifeOS.Application.Interfaces.ICareerWorkflowService _career;

    public ApplicationsController(AppDbContext context, LifeOS.Application.Interfaces.ICareerWorkflowService career)
    {
        _context = context;
        _career = career;
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(sub!);
    }

    [HttpGet]
    public async Task<ActionResult<List<JobApplicationDto>>> GetApplications(CancellationToken ct)
    {
        var userId = GetUserId();
        var applications = await _context.JobApplications
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .Include(a => a.Job)
            .Include(a => a.ResumeVersion)
            .Include(a => a.CoverLetterDocument)
            .Include(a => a.StatusHistory)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

        return Ok(applications.Select(MapApplication));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<JobApplicationDto>> GetApplication(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var application = await _context.JobApplications
            .AsNoTracking()
            .Include(a => a.Job)
            .Include(a => a.ResumeVersion)
            .Include(a => a.CoverLetterDocument)
            .Include(a => a.StatusHistory)
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);

        if (application == null) return NotFound();
        return Ok(MapApplication(application));
    }

    [HttpPost]
    public async Task<ActionResult<JobApplicationDto>> CreateApplication(CreateApplicationRequest request, CancellationToken ct)
    {
        return Ok(await _career.TrackApplicationAsync(GetUserId(),request,ct));
    }

    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult<JobApplicationDto>> UpdateStatus(Guid id, UpdateApplicationStatusRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var application = await _context.JobApplications
            .Include(a => a.Job)
            .Include(a => a.StatusHistory)
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);

        if (application == null) return NotFound();

        if(!new[]{"applied","phone_screen","interview","offer","rejected","withdrawn"}.Contains(request.Status)) return BadRequest("Unknown application status");
        var oldStatus = application.Status;
        application.Status = request.Status;

        // Update job status too
        application.Job.Status = request.Status;

        // Explicitly insert the history row; its UUID is assigned before EF tracks it.
        var history = new ApplicationStatusHistory
        {
            ApplicationId = application.Id,
            Status = request.Status,
            ChangedAt = DateTimeOffset.UtcNow,
            Notes = request.Notes ?? $"Status changed from {oldStatus} to {request.Status}"
        };
        application.StatusHistory.Add(history);
        _context.ApplicationStatusHistories.Add(history);

        await _context.SaveChangesAsync(ct);
        return Ok(MapApplication(application));
    }

    [HttpPost("{id:guid}/notes")]
    public async Task<ActionResult<JobApplicationDto>> AddNotes(Guid id, [FromBody] string notes, CancellationToken ct)
    {
        var userId = GetUserId();
        var application = await _context.JobApplications
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);

        if (application == null) return NotFound();

        application.Notes = application.Notes != null 
            ? application.Notes + "\n" + DateTime.UtcNow.ToString("yyyy-MM-dd") + ": " + notes
            : DateTime.UtcNow.ToString("yyyy-MM-dd") + ": " + notes;

        await _context.SaveChangesAsync(ct);
        return Ok(MapApplication(application));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteApplication(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var application = await _context.JobApplications
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);

        if (application == null) return NotFound();

        var linked=await _context.CalendarEvents.Where(e=>e.ApplicationId==id && e.UserId==userId).ToListAsync(ct);
        _context.CalendarEvents.RemoveRange(linked);
        _context.JobApplications.Remove(application);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    private static JobApplicationDto MapApplication(JobApplication a) => new()
    {
        Id = a.Id,
        JobId = a.JobId,
        JobTitle = a.Job?.Title ?? "",
        Company = a.Job?.Company ?? "",
        RecruiterName=a.RecruiterName, RecruiterEmail=a.RecruiterEmail, RecruiterPhone=a.RecruiterPhone,
        CoverLetterDocumentId=a.CoverLetterDocumentId, CoverLetterDocumentTitle=a.CoverLetterDocument?.Filename,
        Status = a.Status,
        AppliedDate = a.AppliedDate,
        FollowUpDate = a.FollowUpDate,
        Notes = a.Notes,
        ResumeVersionId = a.ResumeVersionId,
        ResumeVersionTitle = a.ResumeVersion?.Title,
        StatusHistory = a.StatusHistory.OrderByDescending(h=>h.ChangedAt).Select(h => new ApplicationStatusHistoryDto
        {
            Status = h.Status,
            ChangedAt = h.ChangedAt,
            Notes = h.Notes
        }).ToList(),
        CreatedAt = a.CreatedAt
    };
}
