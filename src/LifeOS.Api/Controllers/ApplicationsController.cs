using LifeOS.Application.DTOs.Jobs;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ApplicationsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ApplicationsController(AppDbContext context)
    {
        _context = context;
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
            .Include(a => a.StatusHistory)
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);

        if (application == null) return NotFound();
        return Ok(MapApplication(application));
    }

    [HttpPost]
    public async Task<ActionResult<JobApplicationDto>> CreateApplication(CreateApplicationRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        
        // Verify job exists and belongs to user
        var job = await _context.Jobs
            .FirstOrDefaultAsync(j => j.Id == request.JobId && j.UserId == userId, ct);
        
        if (job == null) return BadRequest("Job not found");

        var application = new JobApplication
        {
            UserId = userId,
            JobId = request.JobId,
            ResumeVersionId = request.ResumeVersionId,
            Status = "applied",
            AppliedDate = request.AppliedDate ?? DateTime.UtcNow,
            Notes = request.Notes
        };

        // Update job status
        job.Status = "applied";

        // Add status history
        application.StatusHistory.Add(new ApplicationStatusHistory
        {
            Status = "applied",
            ChangedAt = DateTimeOffset.UtcNow,
            Notes = "Application created"
        });

        _context.JobApplications.Add(application);
        await _context.SaveChangesAsync(ct);

        return Ok(MapApplication(application));
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

        var oldStatus = application.Status;
        application.Status = request.Status;

        // Update job status too
        application.Job.Status = request.Status;

        // Add history entry
        application.StatusHistory.Add(new ApplicationStatusHistory
        {
            Status = request.Status,
            ChangedAt = DateTimeOffset.UtcNow,
            Notes = request.Notes ?? $"Status changed from {oldStatus} to {request.Status}"
        });

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

        _context.JobApplications.Remove(application);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    private static JobApplicationDto MapApplication(JobApplication a) => new()
    {
        Id = a.Id,
        JobId = a.JobId,
        JobTitle = a.Job.Title,
        Company = a.Job.Company,
        Status = a.Status,
        AppliedDate = a.AppliedDate,
        FollowUpDate = a.FollowUpDate,
        Notes = a.Notes,
        ResumeVersionId = a.ResumeVersionId,
        ResumeVersionTitle = a.ResumeVersion?.Title,
        StatusHistory = a.StatusHistory.Select(h => new ApplicationStatusHistoryDto
        {
            Status = h.Status,
            ChangedAt = h.ChangedAt,
            Notes = h.Notes
        }).ToList(),
        CreatedAt = a.CreatedAt
    };
}
