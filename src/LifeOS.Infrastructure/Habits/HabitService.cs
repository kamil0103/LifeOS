using LifeOS.Application.DTOs.Habits;
using LifeOS.Application.Interfaces;
using LifeOS.Application.Services;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using LifeOS.Infrastructure.Personal;
using Microsoft.EntityFrameworkCore;
namespace LifeOS.Infrastructure.Habits;
public class HabitService(AppDbContext db, ILocalDayService days, TimeProvider clock) : IHabitService
{
    private DateTimeOffset Version(DateTimeOffset previous) { var ticks=clock.GetUtcNow().UtcTicks; ticks-=ticks%10; return new DateTimeOffset(Math.Max(ticks, previous.UtcTicks-previous.UtcTicks%10+10),TimeSpan.Zero); }
    private static HabitDto Map(Habit h, LocalDay day, List<HabitCompletion> records) => new()
    {
        TimeZone=day.Zone.Id, Id=h.Id, Name=h.Name, Category=h.Category, TargetValue=h.TargetValue, Unit=h.Unit, Icon=h.Icon, Color=h.Color,
        Frequency=h.ScheduleDaysMask==127?"daily":"custom", ScheduleDaysMask=h.ScheduleDaysMask, IsActive=h.IsActive,
        IsDueToday=HabitSchedule.IsDue(h,day), IsCompleted=records.Any(c=>c.IsCompleted), TodayValue=records.Select(c=>c.Value??0).DefaultIfEmpty().Max(),
        CurrentStreak=HabitSchedule.CurrentStreak(h,day), LongestStreak=h.Streak?.LongestStreak??0, UpdatedAt=h.UpdatedAt
    };
    public async Task<List<HabitDto>> ListAsync(Guid userId, bool archived, bool dueOnly, CancellationToken ct)
    {
        var day=await days.TodayAsync(userId,ct);
        var habits=await db.Habits.AsNoTracking().Include(h=>h.Streak).Where(h=>h.UserId==userId && h.IsActive!=archived).OrderBy(h=>h.Category).ThenBy(h=>h.Name).ToListAsync(ct);
        var records=await db.HabitCompletions.AsNoTracking().Where(c=>c.Habit.UserId==userId && c.CompletedAt>=day.Start && c.CompletedAt<day.End).ToListAsync(ct);
        return habits.Where(h=>!dueOnly||HabitSchedule.IsDue(h,day)).Select(h=>Map(h,day,records.Where(c=>c.HabitId==h.Id).ToList())).ToList();
    }
    public Task<HabitDto?> SaveAsync(Guid userId, Guid? id, HabitSaveRequest request, CancellationToken ct) => PersonalWriteLock.RunAsync<HabitDto?>(db,userId,async()=>
    {
        if(string.IsNullOrWhiteSpace(request.Name)||request.Name.Length>200)throw new InvalidOperationException("Enter a habit name of at most 200 characters.");
        if(string.IsNullOrWhiteSpace(request.Category)||request.Category.Length>80)throw new InvalidOperationException("Enter a category of at most 80 characters.");
        if(request.TargetValue is <=0 or >1000000)throw new InvalidOperationException("The target must be greater than zero and at most 1,000,000.");
        if((request.Unit?.Length??0)>50||(request.Icon?.Length??0)>80||(request.Color?.Length??0)>80)throw new InvalidOperationException("Habit labels are too long.");
        var mask=request.ScheduleDaysMask??127;
        if(mask is <1 or >127)throw new InvalidOperationException("Choose at least one day of the week.");
        if(request.ScheduleDaysMask==null && request.Frequency!=null && request.Frequency!="daily")throw new InvalidOperationException("Choose the days for this schedule.");
        var h=id.HasValue?await db.Habits.Include(h=>h.Streak).FirstOrDefaultAsync(h=>h.Id==id&&h.UserId==userId,ct):new Habit{UserId=userId};
        if(h==null)return null;
        if(request.ExpectedUpdatedAt.HasValue&&h.UpdatedAt!=request.ExpectedUpdatedAt)throw new RecordConflictException("This habit changed. Reload it before saving your edits.");
        h.Name=request.Name.Trim();h.Category=request.Category.Trim();h.TargetValue=request.TargetValue;h.Unit=request.Unit?.Trim();h.Icon=request.Icon;h.Color=request.Color;h.ScheduleDaysMask=mask;h.Frequency=mask==127?"daily":"custom";h.IsActive=request.IsActive;h.UpdatedAt=Version(h.UpdatedAt);
        if(!id.HasValue){db.Habits.Add(h);h.Streak=new Streak{HabitId=h.Id};db.Streaks.Add(h.Streak);}
        var day=await days.TodayAsync(userId,ct);var records=await db.HabitCompletions.Where(c=>c.HabitId==h.Id&&c.CompletedAt>=day.Start&&c.CompletedAt<day.End).ToListAsync(ct);
        return Map(h,day,records);
    },ct);
    public Task<bool> SetActiveAsync(Guid userId, Guid id, bool active, CancellationToken ct)=>PersonalWriteLock.RunAsync(db,userId,async()=>
    {var h=await db.Habits.FirstOrDefaultAsync(h=>h.Id==id&&h.UserId==userId,ct);if(h==null)return false;h.IsActive=active;h.UpdatedAt=Version(h.UpdatedAt);return true;},ct);
    public Task<HabitCompletionResult?> ProgressAsync(Guid userId, Guid id, HabitCompletionRequest? request, bool completeRoute, CancellationToken ct)=>PersonalWriteLock.RunAsync<HabitCompletionResult?>(db,userId,async()=>
    {
        var h=await db.Habits.Include(h=>h.Streak).FirstOrDefaultAsync(h=>h.Id==id&&h.UserId==userId&&h.IsActive,ct);if(h==null)return null;
        var now=clock.GetUtcNow();var profileDay=await days.TodayAsync(userId,ct);var day=LocalDay.At(now,profileDay.Zone.Id);if(!HabitSchedule.IsDue(h,day))throw new InvalidOperationException("This habit is not scheduled for today.");
        if(request?.Value is <0 or >1000000 || (request?.Notes?.Length??0)>2000)throw new InvalidOperationException("Progress must be between 0 and 1,000,000; notes may contain up to 2000 characters.");
        if(h.TargetValue.HasValue && request?.Value==null)throw new InvalidOperationException("Enter today's total progress for this numeric habit.");
        if(!completeRoute&&!h.TargetValue.HasValue)throw new InvalidOperationException("Use Complete for a habit without a numeric target.");
        var records=await db.HabitCompletions.Where(c=>c.HabitId==id&&c.CompletedAt>=day.Start&&c.CompletedAt<day.End).OrderByDescending(c=>c.IsCompleted).ThenBy(c=>c.CreatedAt).ToListAsync(ct);
        var wasComplete=records.Any(c=>c.IsCompleted);if(wasComplete&&completeRoute)throw new InvalidOperationException("Habit already completed today.");
        var current=records.FirstOrDefault();var value=request?.Value;
        if(wasComplete&&value<(current?.Value??0))throw new RecordConflictException("Completed progress cannot be reduced. Today's achievement is preserved.");
        if(current==null){current=new HabitCompletion{HabitId=id,CompletedAt=now,IsCompleted=false};db.HabitCompletions.Add(current);}
        current.Value=value;if(request?.Notes!=null)current.Notes=request.Notes.Trim();current.UpdatedAt=now;current.IsCompleted=wasComplete||!h.TargetValue.HasValue||value>=h.TargetValue;
        var xp=0;var streak=h.Streak;
        if(current.IsCompleted&&!wasComplete)
        {
            if(streak==null){streak=new Streak{HabitId=id};db.Streaks.Add(streak);h.Streak=streak;}
            streak.CurrentStreak=HabitSchedule.CurrentStreak(h,day)+1;streak.LongestStreak=Math.Max(streak.LongestStreak,streak.CurrentStreak);streak.LastCompletedAt=now;
            xp=h.Category.ToLowerInvariant() switch{"coding"=>25,"exercise"=>15,"bible"=>20,"prayer"=>15,"applications"=>30,_=>10};
            db.XpTransactions.Add(new(){UserId=userId,Amount=xp,Source="habit",SourceId=id,Description="Completed habit: "+h.Name});
        }
        var total=await db.XpTransactions.Where(x=>x.UserId==userId).SumAsync(x=>x.Amount,ct);
        return new(streak?.CurrentStreak??0,streak?.LongestStreak??0,xp,total+xp,current.IsCompleted,current.Value??0);
    },ct);
    public async Task<List<HabitHistoryDto>?> HistoryAsync(Guid userId,Guid id,int count,CancellationToken ct)
    {
        if(!await db.Habits.AnyAsync(h=>h.Id==id&&h.UserId==userId,ct))return null;var day=await days.TodayAsync(userId,ct);var since=day.Offset(1-Math.Clamp(count,1,365)).Start;
        return await db.HabitCompletions.AsNoTracking().Where(c=>c.HabitId==id&&c.CompletedAt>=since&&c.CompletedAt<day.End).OrderByDescending(c=>c.CompletedAt).Select(c=>new HabitHistoryDto(c.Id,c.CompletedAt,c.Value,c.Notes,c.IsCompleted)).ToListAsync(ct);
    }
    public async Task<List<StreakDto>> StreaksAsync(Guid userId,CancellationToken ct)
    {var day=await days.TodayAsync(userId,ct);var habits=await db.Habits.AsNoTracking().Include(h=>h.Streak).Where(h=>h.UserId==userId&&h.IsActive).ToListAsync(ct);return habits.Select(h=>new StreakDto(h.Id,h.Name,HabitSchedule.CurrentStreak(h,day),h.Streak?.LongestStreak??0,h.Streak?.LastCompletedAt)).ToList();}
}
