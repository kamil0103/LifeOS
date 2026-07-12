using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly AppDbContext _context;

    public NotificationsController(AppDbContext context)
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
    public async Task<ActionResult<List<object>>> GetNotifications([FromQuery] bool? unread, CancellationToken ct)
    {
        var userId = GetUserId();
        var query = _context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .AsQueryable();

        if (unread.HasValue && unread.Value)
            query = query.Where(n => !n.IsRead);

        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .Select(n => new
            {
                n.Id,
                n.Title,
                n.Message,
                n.Type,
                n.ActionUrl,
                n.IsRead,
                n.CreatedAt
            })
            .ToListAsync(ct);

        return notifications.Cast<object>().ToList();
    }

    [HttpGet("count")]
    public async Task<ActionResult<object>> GetUnreadCount(CancellationToken ct)
    {
        var userId = GetUserId();
        var count = await _context.Notifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId && !n.IsRead, ct);

        return new { unread = count };
    }

    [HttpPost("{id}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, ct);
        if (notification == null)
            return NotFound();

        notification.IsRead = true;
        notification.ReadAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        var userId = GetUserId();
        var unread = await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync(ct);

        foreach (var n in unread)
        {
            n.IsRead = true;
            n.ReadAt = DateTimeOffset.UtcNow;
        }

        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteNotification(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, ct);
        if (notification == null)
            return NotFound();

        _context.Notifications.Remove(notification);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    // Internal helper used by other controllers
    public async Task CreateNotification(Guid userId, string title, string message, string type, string? actionUrl, CancellationToken ct)
    {
        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            ActionUrl = actionUrl
        };
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync(ct);
    }
}
