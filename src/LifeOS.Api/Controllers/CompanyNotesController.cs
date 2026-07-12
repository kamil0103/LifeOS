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
public class CompanyNotesController : ControllerBase
{
    private readonly AppDbContext _context;

    public CompanyNotesController(AppDbContext context)
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
    public async Task<ActionResult<List<CompanyNoteDto>>> GetNotes(CancellationToken ct)
    {
        var userId = GetUserId();
        var notes = await _context.CompanyNotes
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(ct);

        return Ok(notes.Select(MapNote));
    }

    [HttpPost]
    public async Task<ActionResult<CompanyNoteDto>> CreateNote(CreateCompanyNoteRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var note = new CompanyNote
        {
            UserId = userId,
            CompanyName = request.CompanyName,
            Notes = request.Notes,
            Rating = request.Rating
        };

        _context.CompanyNotes.Add(note);
        await _context.SaveChangesAsync(ct);
        return Ok(MapNote(note));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CompanyNoteDto>> UpdateNote(Guid id, UpdateCompanyNoteRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var note = await _context.CompanyNotes
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, ct);

        if (note == null) return NotFound();

        note.CompanyName = request.CompanyName;
        note.Notes = request.Notes;
        note.Rating = request.Rating;

        await _context.SaveChangesAsync(ct);
        return Ok(MapNote(note));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteNote(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var note = await _context.CompanyNotes
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, ct);

        if (note == null) return NotFound();

        _context.CompanyNotes.Remove(note);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    private static CompanyNoteDto MapNote(CompanyNote n) => new()
    {
        Id = n.Id,
        CompanyName = n.CompanyName,
        Notes = n.Notes,
        Rating = n.Rating,
        CreatedAt = n.CreatedAt
    };
}
