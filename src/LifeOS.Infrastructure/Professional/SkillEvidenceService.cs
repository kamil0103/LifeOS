using LifeOS.Application.DTOs.Skills;
using LifeOS.Application.Interfaces;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using LifeOS.Infrastructure.Personal;
using Microsoft.EntityFrameworkCore;
namespace LifeOS.Infrastructure.Professional;
public class SkillEvidenceService(AppDbContext db) : ISkillEvidenceService
{
    public async Task<List<EvidenceSourceDto>> SourcesAsync(Guid userId,CancellationToken ct)
    {
        var result=new List<EvidenceSourceDto>();
        result.AddRange((await db.WorkExperiences.AsNoTracking().Where(x=>x.UserId==userId).OrderBy(x=>x.Company).ToListAsync(ct)).Select(x=>new EvidenceSourceDto("work",x.Id,x.Title+" at "+x.Company,"/experience")));
        result.AddRange((await db.Projects.AsNoTracking().Where(x=>x.UserId==userId).OrderBy(x=>x.Name).ToListAsync(ct)).Select(x=>new EvidenceSourceDto("project",x.Id,x.Name,"/experience")));
        result.AddRange((await db.Courses.AsNoTracking().Where(x=>x.UserId==userId).OrderBy(x=>x.Name).ToListAsync(ct)).Select(x=>new EvidenceSourceDto("course",x.Id,(x.Code+" "+x.Name+" "+x.Term).Trim(),"/education")));
        result.AddRange((await db.Certificates.AsNoTracking().Where(x=>x.UserId==userId).OrderBy(x=>x.Name).ToListAsync(ct)).Select(x=>new EvidenceSourceDto("certificate",x.Id,x.Name,"/skills?tab=certificates")));
        return result;
    }
    private static SkillEvidenceDto Map(SkillEvidence e,List<EvidenceSourceDto> sources)
    {var source=sources.FirstOrDefault(s=>s.Type==e.SourceType&&s.Id==e.SourceId);return new(e.Id,e.SourceType,e.SourceId,e.Notes,source?.Label??"Source no longer available",source?.Url,source!=null);}
    public async Task<List<SkillEvidenceDto>?> ListAsync(Guid userId,Guid skillId,CancellationToken ct)
    {
        if(!await db.Skills.AnyAsync(s=>s.UserId==userId&&s.Id==skillId,ct))return null;
        var rows=await db.SkillEvidence.AsNoTracking().Where(e=>e.UserId==userId&&e.SkillId==skillId).OrderBy(e=>e.CreatedAt).ToListAsync(ct);var sources=await SourcesAsync(userId,ct);
        return rows.Select(e=>Map(e,sources)).ToList();
    }
    public Task<SkillEvidenceDto?> AddAsync(Guid userId,Guid skillId,SaveSkillEvidenceRequest request,CancellationToken ct)=>PersonalWriteLock.RunAsync<SkillEvidenceDto?>(db,userId,async()=>
    {
        if(!await db.Skills.AnyAsync(s=>s.UserId==userId&&s.Id==skillId,ct))return null;
        if((request.Notes?.Length??0)>1500)throw new InvalidOperationException("Evidence notes may contain up to 1500 characters.");
        var sources=await SourcesAsync(userId,ct);if(!sources.Any(s=>s.Type==request.SourceType&&s.Id==request.SourceId))throw new InvalidOperationException("Select one of your own work records, projects, courses or certificates.");
        var row=await db.SkillEvidence.FirstOrDefaultAsync(e=>e.UserId==userId&&e.SkillId==skillId&&e.SourceType==request.SourceType&&e.SourceId==request.SourceId,ct);
        if(row==null){row=new(){UserId=userId,SkillId=skillId,SourceType=request.SourceType,SourceId=request.SourceId,Notes=request.Notes?.Trim()};db.SkillEvidence.Add(row);}
        return Map(row,sources);
    },ct);
    public Task<bool> RemoveAsync(Guid userId,Guid skillId,Guid evidenceId,CancellationToken ct)=>PersonalWriteLock.RunAsync(db,userId,async()=>
    {var row=await db.SkillEvidence.FirstOrDefaultAsync(e=>e.UserId==userId&&e.SkillId==skillId&&e.Id==evidenceId,ct);if(row==null)return false;db.SkillEvidence.Remove(row);return true;},ct);
}
