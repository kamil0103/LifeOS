using System.Collections.Concurrent;
using LifeOS.Application.DTOs.Jobs;
using LifeOS.Application.Interfaces;
using LifeOS.Application.Services;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace LifeOS.Infrastructure.Career;
public class CareerWorkflowService(AppDbContext db) : ICareerWorkflowService
{
    public async Task<JobDto?> UpdateJobAsync(Guid userId,Guid jobId,UpdateJobRequest request,CancellationToken ct)
    {
        var job=await db.Jobs.FirstOrDefaultAsync(j=>j.Id==jobId&&j.UserId==userId,ct);if(job==null)return null;
        if(string.IsNullOrWhiteSpace(request.Title)||request.Title.Length>200||string.IsNullOrWhiteSpace(request.Company)||request.Company.Length>200)throw new InvalidOperationException("Enter a job title and company of at most 200 characters.");
        if((request.Description?.Length??0)>100000)throw new InvalidOperationException("Job description is too long.");
        var url=JobIdentity.CanonicalUrl(request.Url);var status=string.IsNullOrWhiteSpace(request.Status)?job.Status:request.Status;
        if(!new[]{"saved","applied","phone_screen","interview","offer","rejected","withdrawn"}.Contains(status))throw new InvalidOperationException("Unknown job status.");
        if(request.MatchScore is <0 or >100)throw new InvalidOperationException("Match score must be between 0 and 100.");
        if(status!=job.Status && await db.JobApplications.AnyAsync(a=>a.JobId==jobId,ct))throw new InvalidOperationException("Change this job's application status in the Application Tracker.");
        job.Title=request.Title.Trim();job.Company=request.Company.Trim();job.Description=request.Description??"";job.Location=request.Location?.Trim();job.Url=url;job.SalaryRange=request.SalaryRange;job.JobType=request.JobType;job.PostedDate=request.PostedDate?.ToUniversalTime();job.MatchScore=request.MatchScore;job.Status=status;job.UpdatedAt=DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);return Map(job);
    }
    public async Task<bool> DeleteJobAsync(Guid userId,Guid jobId,CancellationToken ct)
    {
        var gate=Gates.GetOrAdd(userId,_=>new(1,1));await gate.WaitAsync(ct);
        try{
            await using var transaction=await LockAsync(userId,ct);
            var job=await db.Jobs.FirstOrDefaultAsync(j=>j.Id==jobId&&j.UserId==userId,ct);if(job==null)return false;
            if(await db.JobApplications.AnyAsync(a=>a.JobId==jobId,ct))throw new InvalidOperationException("This job has a tracked application. Remove the application first if you also want to delete its job.");
            foreach(var document in await db.Documents.Where(d=>d.JobId==jobId).ToListAsync(ct))document.JobId=null;
            foreach(var item in await db.CalendarEvents.Where(e=>e.JobId==jobId).ToListAsync(ct))item.JobId=null;
            db.Jobs.Remove(job);await db.SaveChangesAsync(ct);if(transaction!=null)await transaction.CommitAsync(ct);return true;
        }finally{gate.Release();}
    }
    public async Task EnsureAssetUnusedAsync(Guid assetId,bool isResume,CancellationToken ct)
    {
        var used=isResume?await db.JobApplications.AnyAsync(a=>a.ResumeVersionId==assetId,ct):await db.JobApplications.AnyAsync(a=>a.CoverLetterDocumentId==assetId,ct);
        if(used)throw new InvalidOperationException("This " +(isResume?"resume version":"document")+" is linked to an application. Remove the link in the application before deleting it.");
    }
    private static readonly ConcurrentDictionary<Guid,SemaphoreSlim> Gates=new();
    private async Task<IDbContextTransaction?> LockAsync(Guid userId,CancellationToken ct)
    {
        if(!db.Database.IsRelational())return null;
        var transaction=await db.Database.BeginTransactionAsync(ct);
        try{var key=BitConverter.ToInt64(userId.ToByteArray(),0);await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})",ct);return transaction;}
        catch{await transaction.DisposeAsync();throw;}
    }
    public async Task<JobDto> SaveJobAsync(Guid userId,CreateJobRequest request,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(request.Title)||request.Title.Length>200||string.IsNullOrWhiteSpace(request.Company)||request.Company.Length>200)throw new InvalidOperationException("Enter a job title and company of at most 200 characters.");
        if((request.Description?.Length??0)>100000)throw new InvalidOperationException("Job description is too long.");
        var url=JobIdentity.CanonicalUrl(request.Url);var gate=Gates.GetOrAdd(userId,_=>new(1,1));await gate.WaitAsync(ct);
        try{
            await using var transaction=await LockAsync(userId,ct);
            var existing=await db.Jobs.AsNoTracking().Where(j=>j.UserId==userId).OrderBy(j=>j.CreatedAt).ToListAsync(ct);
            var duplicate=existing.FirstOrDefault(j=>SameJob(j,request,url));
            if(duplicate!=null){if(transaction!=null)await transaction.CommitAsync(ct);return Map(duplicate);}
            var job=new Job{UserId=userId,Title=request.Title.Trim(),Company=request.Company.Trim(),Location=request.Location?.Trim(),Description=request.Description??"",Url=url,Source=request.Source??"manual",SalaryRange=request.SalaryRange,JobType=request.JobType,PostedDate=request.PostedDate?.ToUniversalTime(),Status="saved"};
            db.Jobs.Add(job);await db.SaveChangesAsync(ct);if(transaction!=null)await transaction.CommitAsync(ct);return Map(job);
        }finally{gate.Release();}
    }
    private static bool SameJob(Job job,CreateJobRequest request,string? url)
    {
        string? oldUrl=null;try{oldUrl=JobIdentity.CanonicalUrl(job.Url);}catch(InvalidOperationException){}
        if(url!=null && oldUrl!=null)return url==oldUrl;
        return JobIdentity.Text(job.Title)==JobIdentity.Text(request.Title)&&JobIdentity.Text(job.Company)==JobIdentity.Text(request.Company)&&JobIdentity.Text(job.Location)==JobIdentity.Text(request.Location);
    }
    public async Task<JobApplicationDto> TrackApplicationAsync(Guid userId,CreateApplicationRequest request,CancellationToken ct)
    {
        if((request.Notes?.Length??0)>20000)throw new InvalidOperationException("Application notes are too long.");
        var gate=Gates.GetOrAdd(userId,_=>new(1,1));await gate.WaitAsync(ct);
        try{
            await using var transaction=await LockAsync(userId,ct);
            var job=await db.Jobs.FirstOrDefaultAsync(j=>j.Id==request.JobId&&j.UserId==userId,ct)??throw new InvalidOperationException("Job not found.");
            if(request.ResumeVersionId.HasValue&&!await db.ResumeVersions.AnyAsync(r=>r.Id==request.ResumeVersionId&&r.UserId==userId,ct))throw new InvalidOperationException("Resume version not found.");
            var existing=await db.JobApplications.Where(a=>a.JobId==job.Id&&a.UserId==userId).Include(a=>a.Job).Include(a=>a.ResumeVersion).Include(a=>a.CoverLetterDocument).Include(a=>a.StatusHistory).OrderBy(a=>a.CreatedAt).FirstOrDefaultAsync(ct);
            if(existing!=null){if(transaction!=null)await transaction.CommitAsync(ct);return Map(existing);}
            var application=new JobApplication{UserId=userId,JobId=job.Id,Job=job,ResumeVersionId=request.ResumeVersionId,Status="applied",AppliedDate=request.AppliedDate?.ToUniversalTime()??DateTime.UtcNow,Notes=request.Notes};
            application.StatusHistory.Add(new(){Status="applied",Notes="Application tracked"});job.Status="applied";job.UpdatedAt=DateTimeOffset.UtcNow;db.JobApplications.Add(application);await db.SaveChangesAsync(ct);
            if(request.ResumeVersionId.HasValue)application.ResumeVersion=await db.ResumeVersions.FindAsync([request.ResumeVersionId.Value],ct);
            if(transaction!=null)await transaction.CommitAsync(ct);return Map(application);
        }finally{gate.Release();}
    }
    private static JobDto Map(Job j)=>new(){Id=j.Id,Title=j.Title,Company=j.Company,Location=j.Location,Description=j.Description,Url=j.Url,Source=j.Source,SalaryRange=j.SalaryRange,JobType=j.JobType,PostedDate=j.PostedDate,MatchScore=j.MatchScore,Status=j.Status,CreatedAt=j.CreatedAt};
    private static JobApplicationDto Map(JobApplication a)=>new(){Id=a.Id,JobId=a.JobId,JobTitle=a.Job.Title,Company=a.Job.Company,Status=a.Status,AppliedDate=a.AppliedDate,FollowUpDate=a.FollowUpDate,Notes=a.Notes,ResumeVersionId=a.ResumeVersionId,ResumeVersionTitle=a.ResumeVersion?.Title,CoverLetterDocumentId=a.CoverLetterDocumentId,CoverLetterDocumentTitle=a.CoverLetterDocument?.Filename,RecruiterName=a.RecruiterName,RecruiterEmail=a.RecruiterEmail,RecruiterPhone=a.RecruiterPhone,CreatedAt=a.CreatedAt,StatusHistory=a.StatusHistory.OrderByDescending(h=>h.ChangedAt).Select(h=>new ApplicationStatusHistoryDto{Status=h.Status,ChangedAt=h.ChangedAt,Notes=h.Notes}).ToList()};
}
