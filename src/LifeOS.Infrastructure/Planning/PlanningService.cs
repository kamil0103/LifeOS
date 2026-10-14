using System.Text.Json;
using LifeOS.Application.DTOs.Calendar;
using LifeOS.Application.DTOs.Planning;
using LifeOS.Application.Interfaces;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace LifeOS.Infrastructure.Planning;
public class PlanningService(AppDbContext db,ILocalDayService days,ICalendarService calendar,IGoogleCalendarService google) : IPlanningService
{
    private static readonly JsonSerializerOptions Json=new(){PropertyNameCaseInsensitive=true};
    public static int ReviewDays(int attempts)=>attempts switch{<=1=>1,2=>3,3=>7,4=>14,_=>30};
    public async Task<DailyPlanDto> TodayAsync(Guid userId,CancellationToken ct)
    {
        var day=await days.TodayAsync(userId,ct);var start=day.Start;var end=day.End;
        var result=new DailyPlanDto{Date=day.Date.ToString("yyyy-MM-dd"),TimeZone=day.Zone.Id};
        var habits=await db.Habits.AsNoTracking().Where(h=>h.UserId==userId && h.IsActive)
            .Select(h=>new{h.Id,h.Name,h.Category,Completed=h.Completions.Any(c=>c.CompletedAt>=start && c.CompletedAt<end)}).ToListAsync(ct);
        result.Actions.AddRange(habits.Select(h=>new DailyActionDto("habit:"+h.Id,"habit",h.Id,h.Name,h.Category,2,h.Completed,null,"/habits",true)));
        var applications=await db.JobApplications.AsNoTracking().Where(a=>a.UserId==userId && a.FollowUpDate!=null && a.FollowUpDate<end.UtcDateTime && a.Status!="rejected" && a.Status!="withdrawn").Include(a=>a.Job).ToListAsync(ct);
        result.Actions.AddRange(applications.Select(a=>new DailyActionDto("followup:"+a.Id,"followup",a.Id,"Follow up with "+a.Job.Company,a.Job.Title,a.FollowUpDate<start.UtcDateTime?0:1,false,new DateTimeOffset(DateTime.SpecifyKind(a.FollowUpDate!.Value,DateTimeKind.Utc)),"/jobs/applications?application="+a.Id,true)));
        var mission=await db.DailyMissions.AsNoTracking().FirstOrDefaultAsync(m=>m.UserId==userId && m.MissionDate.Date==day.Date,ct);
        if(mission!=null){var items=JsonSerializer.Deserialize<List<MissionItem>>(mission.PrioritiesJson,Json)??[];result.Actions.AddRange(items.Select((item,index)=>new DailyActionDto($"mission:{mission.Id}:{index}","mission",mission.Id,item.Title,item.Reason,item.Priority.ToLowerInvariant()=="high"?1:item.Priority.ToLowerInvariant()=="low"?3:2,item.Completed||mission.CompletedAt.HasValue,null,"/ai-coach",true)));}
        var problems=await db.CodingProblems.AsNoTracking().Where(p=>p.UserId==userId && p.IsSolved && p.SolvedAt.HasValue).Select(p=>new{p.Id,p.Title,p.SolvedAt,Attempts=p.Attempts.Count}).ToListAsync(ct);
        foreach(var p in problems){var due=day.DateOf(p.SolvedAt!.Value).AddDays(ReviewDays(p.Attempts));if(due<=day.Date)result.Actions.Add(new("review:"+p.Id,"review",p.Id,"Review: "+p.Title,"Revisit your solution and explain the approach.",2,false,day.Offset((int)(due-day.Date).TotalDays).Start,"/coding?problem="+p.Id,true));}
        var local=await calendar.GetAsync(userId,start,end,ct);
        result.Commitments.AddRange(local.Select(e=>new CommitmentDto(e.Id.ToString(),e.Title,e.StartTime,e.EndTime,e.IsAllDay,"LifeOS local","/calendar?date="+day.DateOf(e.StartTime).ToString("yyyy-MM-dd")+"&event="+e.Id)));
        try{var external=await google.EventsAsync(userId,start,end,false,ct);result.Commitments.AddRange(external.Select(e=>new CommitmentDto(e.Id,e.Title,e.StartTime,e.EndTime,e.IsAllDay,e.CalendarName,"/calendar?date="+(e.StartDate??day.DateOf(e.StartTime).ToString("yyyy-MM-dd"))+"&event="+Uri.EscapeDataString(e.Id))));}
        catch(Exception)when(!ct.IsCancellationRequested){result.GoogleWarning="Google commitments could not be refreshed. Check Calendar connection status.";}
        result.Actions=result.Actions.OrderBy(a=>a.Completed).ThenBy(a=>a.Priority).ThenBy(a=>a.DueAt??day.End).ThenBy(a=>a.Title).ToList();
        result.Commitments=result.Commitments.OrderBy(c=>c.StartTime).ToList();return result;
    }
    public async Task<bool> CompleteAsync(Guid userId,string actionId,CancellationToken ct)
    {
        var parts=actionId.Split(':');if(parts.Length<2 || !Guid.TryParse(parts[1],out var id))return false;
        if(parts[0]=="followup")return await FollowUpAsync(userId,id,new(null,"Follow-up completed."),ct);
        if(parts[0]=="mission" && parts.Length==3 && int.TryParse(parts[2],out var index)){
            await using var transaction=await db.Database.BeginTransactionAsync(ct);
            var count=await db.DailyMissions.Where(m=>m.Id==id && m.UserId==userId).ExecuteUpdateAsync(s=>s.SetProperty(m=>m.UpdatedAt,DateTimeOffset.UtcNow),ct);if(count==0)return false;
            var mission=await db.DailyMissions.SingleAsync(m=>m.Id==id && m.UserId==userId,ct);var items=JsonSerializer.Deserialize<List<MissionItem>>(mission.PrioritiesJson,Json)??[];
            if(index<0 || index>=items.Count)return false;items[index].Completed=true;mission.PrioritiesJson=JsonSerializer.Serialize(items);if(items.All(i=>i.Completed))mission.CompletedAt=DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);return true;
        }
        if(parts[0]=="review"){
            var day=await days.TodayAsync(userId,ct);await using var transaction=await db.Database.BeginTransactionAsync(ct);
            var count=await db.CodingProblems.Where(p=>p.Id==id && p.UserId==userId && p.IsSolved).ExecuteUpdateAsync(s=>s.SetProperty(p=>p.UpdatedAt,DateTimeOffset.UtcNow),ct);if(count==0)return false;
            var problem=await db.CodingProblems.SingleAsync(p=>p.Id==id && p.UserId==userId,ct);
            if(problem.SolvedAt>=day.Start){await transaction.CommitAsync(ct);return true;}
            problem.SolvedAt=DateTimeOffset.UtcNow;db.ProblemAttempts.Add(new(){ProblemId=id,SolvedAt=problem.SolvedAt.Value,Notes="Reviewed from the daily plan.",XpEarned=0});await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);return true;
        }
        return false;
    }
    public async Task<ScheduledActionResult> ScheduleAsync(Guid userId,ScheduleActionRequest request,CancellationToken ct)
    {
        Calendar.CalendarService.Validate("Scheduled action",request.StartTime,request.EndTime);
        var plan=await TodayAsync(userId,ct);var action=plan.Actions.FirstOrDefault(a=>a.Id==request.ActionId && !a.Completed)??throw new InvalidOperationException("This action is no longer pending. Refresh your daily plan.");
        var actionKey=action.Id+":"+(action.DueAt?.ToUnixTimeSeconds().ToString()??plan.Date);
        if(request.Destination=="google"){
            try{var item=await google.CreateAsync(userId,new(){Title=action.Title,Description=action.Reason,StartTime=request.StartTime,EndTime=request.EndTime,ActionKey=actionKey},ct);return new(item.Id,"/calendar?date="+(await days.TodayAsync(userId,ct)).DateOf(item.StartTime).ToString("yyyy-MM-dd")+"&event="+Uri.EscapeDataString(item.Id),false);}
            catch(CalendarEventAlreadyExistsException){return new("","/calendar",true);}
        }
        if(request.Destination!="local")throw new InvalidOperationException("Choose a supported calendar.");
        if(action.Kind=="followup" && action.SourceId.HasValue) {
            await FollowUpAsync(userId,action.SourceId.Value,new(request.StartTime,"Follow-up scheduled from the daily plan.",request.EndTime),ct);
            var linked=await db.CalendarEvents.AsNoTracking().SingleAsync(e=>e.UserId==userId && e.ApplicationId==action.SourceId && e.EventType=="followup",ct);
            return new(linked.Id.ToString(),"/calendar?event="+linked.Id+"&date="+(await days.TodayAsync(userId,ct)).DateOf(linked.StartTime).ToString("yyyy-MM-dd"),false);
        }
        var existing=await db.CalendarEvents.AsNoTracking().FirstOrDefaultAsync(e=>e.UserId==userId && e.ActionKey==actionKey,ct);
        if(existing!=null)return new(existing.Id.ToString(),"/calendar?event="+existing.Id+"&date="+(await days.TodayAsync(userId,ct)).DateOf(existing.StartTime).ToString("yyyy-MM-dd"),true);
        CalendarEventDto created;
        try { created=await calendar.CreateAsync(userId,new(){Title=action.Title,Description=action.Reason,ActionKey=actionKey,StartTime=request.StartTime,EndTime=request.EndTime,EventType=action.Kind=="review"?"coding":action.Kind,HabitId=action.Kind=="habit"?action.SourceId:null},ct); }
        catch(DbUpdateException ex) when(ex.InnerException is PostgresException pg && pg.SqlState==PostgresErrorCodes.UniqueViolation) {
            foreach(var entry in db.ChangeTracker.Entries<CalendarEvent>().Where(e=>e.State==EntityState.Added).ToList()) entry.State=EntityState.Detached;
            var duplicate=await db.CalendarEvents.AsNoTracking().SingleAsync(e=>e.UserId==userId && e.ActionKey==actionKey,ct);
            return new(duplicate.Id.ToString(),"/calendar?event="+duplicate.Id+"&date="+(await days.TodayAsync(userId,ct)).DateOf(duplicate.StartTime).ToString("yyyy-MM-dd"),true);
        }
        return new(created.Id.ToString(),"/calendar?event="+created.Id+"&date="+(await days.TodayAsync(userId,ct)).DateOf(created.StartTime).ToString("yyyy-MM-dd"),false);
    }
    public async Task<bool> FollowUpAsync(Guid userId,Guid applicationId,FollowUpRequest request,CancellationToken ct)
    {
        if(request.DueAt.HasValue) Calendar.CalendarService.Validate("Follow-up",request.DueAt.Value,request.EndAt);
        await using var transaction=db.Database.IsRelational()?await db.Database.BeginTransactionAsync(ct):null;
        if(db.Database.IsRelational()) await db.JobApplications.Where(a=>a.Id==applicationId && a.UserId==userId).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.UpdatedAt,DateTimeOffset.UtcNow),ct);
        var application=await db.JobApplications.Include(a=>a.Job).FirstOrDefaultAsync(a=>a.Id==applicationId && a.UserId==userId,ct);if(application==null)return false;
        application.FollowUpDate=request.DueAt?.UtcDateTime;application.UpdatedAt=DateTimeOffset.UtcNow;
        var linked=await db.CalendarEvents.FirstOrDefaultAsync(e=>e.UserId==userId && e.ApplicationId==applicationId && e.EventType=="followup",ct);
        if(request.DueAt.HasValue){
            if(linked==null){linked=new(){UserId=userId,ApplicationId=applicationId,JobId=application.JobId,EventType="followup"};db.CalendarEvents.Add(linked);}
            linked.Title="Follow up with "+application.Job.Company;linked.Description=application.Job.Title;linked.StartTime=request.DueAt.Value.ToUniversalTime();linked.EndTime=request.EndAt?.ToUniversalTime()??linked.StartTime.AddMinutes(30);linked.UpdatedAt=DateTimeOffset.UtcNow;
        }else if(linked!=null)db.CalendarEvents.Remove(linked);
        if(!string.IsNullOrWhiteSpace(request.Notes))db.ApplicationStatusHistories.Add(new(){ApplicationId=application.Id,Status=application.Status,ChangedAt=DateTimeOffset.UtcNow,Notes=request.Notes.Trim()});
        await db.SaveChangesAsync(ct);if(transaction!=null)await transaction.CommitAsync(ct);return true;
    }
    public async Task<bool> DetailsAsync(Guid userId,Guid applicationId,ApplicationDetailsRequest request,CancellationToken ct)
    {
        var application=await db.JobApplications.FirstOrDefaultAsync(a=>a.Id==applicationId && a.UserId==userId,ct);if(application==null)return false;
        if(request.ResumeVersionId.HasValue && !await db.ResumeVersions.AnyAsync(r=>r.Id==request.ResumeVersionId && r.UserId==userId,ct))throw new InvalidOperationException("Resume version not found.");
        if(request.CoverLetterDocumentId.HasValue && !await db.Documents.AnyAsync(d=>d.Id==request.CoverLetterDocumentId && d.UserId==userId && d.Type=="cover_letter",ct))throw new InvalidOperationException("Cover letter not found.");
        if((request.RecruiterName?.Length??0)>200 || (request.RecruiterEmail?.Length??0)>320 || (request.RecruiterPhone?.Length??0)>80)throw new InvalidOperationException("Recruiter details are too long.");
        application.RecruiterName=request.RecruiterName?.Trim();application.RecruiterEmail=request.RecruiterEmail?.Trim();application.RecruiterPhone=request.RecruiterPhone?.Trim();application.ResumeVersionId=request.ResumeVersionId;application.CoverLetterDocumentId=request.CoverLetterDocumentId;application.UpdatedAt=DateTimeOffset.UtcNow;await db.SaveChangesAsync(ct);return true;
    }
}
