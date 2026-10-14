using System.Collections.Concurrent;
using LifeOS.Application.DTOs.Planning;
using LifeOS.Application.Interfaces;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace LifeOS.Infrastructure.Planning;
public class ReminderService(AppDbContext db) : IReminderService
{
    private static readonly ConcurrentDictionary<Guid,SemaphoreSlim> Gates=new();
    public async Task<ReminderPreferencesDto> PreferencesAsync(Guid userId,CancellationToken ct)
    {var p=await db.ReminderPreferences.AsNoTracking().FirstOrDefaultAsync(p=>p.UserId==userId,ct);return p==null?new(true,true,true,30):new(p.FollowUpsEnabled,p.CalendarEnabled,p.CodingReviewsEnabled,p.CalendarLeadMinutes);}
    public async Task SavePreferencesAsync(Guid userId,ReminderPreferencesDto request,CancellationToken ct)
    {
        if(request.CalendarLeadMinutes<0 || request.CalendarLeadMinutes>1440)throw new InvalidOperationException("Calendar reminder lead time must be between 0 and 1440 minutes.");
        var p=await db.ReminderPreferences.FirstOrDefaultAsync(p=>p.UserId==userId,ct);if(p==null){p=new(){UserId=userId};db.ReminderPreferences.Add(p);}
        p.FollowUpsEnabled=request.FollowUpsEnabled;p.CalendarEnabled=request.CalendarEnabled;p.CodingReviewsEnabled=request.CodingReviewsEnabled;p.CalendarLeadMinutes=request.CalendarLeadMinutes;await db.SaveChangesAsync(ct);
    }
    public async Task RemindAsync(Guid userId,DailyPlanDto plan,CancellationToken ct)
    {
        var gate=Gates.GetOrAdd(userId,_=>new(1,1));await gate.WaitAsync(ct);
        try{
            var prefs=await PreferencesAsync(userId,ct);var now=DateTimeOffset.UtcNow;var candidates=new List<Notification>();
            foreach(var action in plan.Actions.Where(a=>!a.Completed && a.DueAt<=now && (a.Kind=="followup"&&prefs.FollowUpsEnabled || a.Kind=="review"&&prefs.CodingReviewsEnabled)))
                candidates.Add(new(){UserId=userId,SourceKey=action.Id+":"+action.DueAt!.Value.ToUnixTimeSeconds(),Title=action.Title,Message=action.Reason,Type="reminder",ActionUrl=action.ActionUrl});
            if(prefs.CalendarEnabled)foreach(var commitment in plan.Commitments.Where(c=>c.StartTime<=now.AddMinutes(prefs.CalendarLeadMinutes) && (c.EndTime??c.StartTime.AddHours(1))>now))
                candidates.Add(new(){UserId=userId,SourceKey="calendar:"+commitment.Id+":"+commitment.StartTime.ToUnixTimeSeconds(),Title=commitment.Title,Message=commitment.IsAllDay?"On your calendar today.":"Starts at "+TimeZoneInfo.ConvertTime(commitment.StartTime,LifeOS.Application.Services.LocalDay.At(now,plan.TimeZone).Zone).ToString("h:mm tt"),Type="reminder",ActionUrl=commitment.ActionUrl});
            var keys=candidates.Select(n=>n.SourceKey).ToHashSet();
            var existing=await db.Notifications.Where(n=>n.UserId==userId && n.SourceKey!=null).ToListAsync(ct);
            foreach(var n in existing.Where(n=>!n.IsRead && !n.IsArchived && !keys.Contains(n.SourceKey) && !(plan.GoogleWarning!=null && n.SourceKey!.StartsWith("calendar:google:")))){n.IsArchived=true;n.IsRead=true;n.ReadAt=now;}
            await db.SaveChangesAsync(ct);
            var seen=existing.Select(n=>n.SourceKey).ToHashSet();
            foreach(var n in candidates.Where(n=>!seen.Contains(n.SourceKey))){
                db.Notifications.Add(n);
                try{await db.SaveChangesAsync(ct);seen.Add(n.SourceKey);}
                catch(DbUpdateException ex)when(ex.InnerException is PostgresException p && p.SqlState==PostgresErrorCodes.UniqueViolation){db.Entry(n).State=EntityState.Detached;}
            }
        }finally{gate.Release();}
    }
    private IQueryable<Notification> Visible(Guid userId)
    {var now=DateTimeOffset.UtcNow;return db.Notifications.Where(n=>n.UserId==userId && !n.IsArchived && (!n.SnoozedUntil.HasValue || n.SnoozedUntil<=now));}
    public async Task<List<Notification>> ListAsync(Guid userId,bool unread,int page,CancellationToken ct)
    {var query=Visible(userId).AsNoTracking();if(unread)query=query.Where(n=>!n.IsRead);return await query.OrderByDescending(n=>n.CreatedAt).Skip((Math.Clamp(page,1,1000)-1)*50).Take(50).ToListAsync(ct);}
    public Task<int> CountAsync(Guid userId,CancellationToken ct)=>Visible(userId).CountAsync(n=>!n.IsRead,ct);
    public async Task<bool> ReadAsync(Guid userId,Guid id,CancellationToken ct)
    {var n=await db.Notifications.FirstOrDefaultAsync(n=>n.Id==id && n.UserId==userId,ct);if(n==null)return false;n.IsRead=true;n.ReadAt=DateTimeOffset.UtcNow;await db.SaveChangesAsync(ct);return true;}
    public async Task ReadAllAsync(Guid userId,CancellationToken ct)
    {var list=await Visible(userId).Where(n=>!n.IsRead).ToListAsync(ct);foreach(var n in list){n.IsRead=true;n.ReadAt=DateTimeOffset.UtcNow;}await db.SaveChangesAsync(ct);}
    public async Task<bool> ArchiveAsync(Guid userId,Guid id,CancellationToken ct)
    {var n=await db.Notifications.FirstOrDefaultAsync(n=>n.Id==id && n.UserId==userId,ct);if(n==null)return false;n.IsArchived=true;n.IsRead=true;n.ReadAt=DateTimeOffset.UtcNow;await db.SaveChangesAsync(ct);return true;}
    public async Task<bool> SnoozeAsync(Guid userId,Guid id,int minutes,CancellationToken ct)
    {
        if(minutes<5 || minutes>10080)throw new InvalidOperationException("Snooze must be between five minutes and seven days.");
        var n=await db.Notifications.FirstOrDefaultAsync(n=>n.Id==id && n.UserId==userId && !n.IsArchived,ct);if(n==null)return false;n.SnoozedUntil=DateTimeOffset.UtcNow.AddMinutes(minutes);await db.SaveChangesAsync(ct);return true;
    }
}
