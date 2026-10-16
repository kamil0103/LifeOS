using System.Net;
using System.Net.Http.Json;
using LifeOS.Application.DTOs.Skills;
using LifeOS.Application.DTOs.Experience;
using LifeOS.Application.DTOs.Planning;
using LifeOS.Application.Interfaces;
using LifeOS.Application.Services;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using LifeOS.Infrastructure.Professional;
using LifeOS.Infrastructure.Planning;
using LifeOS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
namespace LifeOS.Tests;
public class EvidenceCareerTests
{
    private class Fixture : IDisposable
    {
        public AppDbContext Db=new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);public Guid User=Guid.NewGuid();public ProfessionalRecordService Records;public SkillEvidenceService Evidence;
        public Fixture(){var days=new Mock<ILocalDayService>();days.Setup(x=>x.TodayAsync(It.IsAny<Guid>(),It.IsAny<CancellationToken>())).ReturnsAsync(LocalDay.At(DateTimeOffset.UtcNow,"America/Los_Angeles"));Records=new(Db,new Mock<IGitHubRepositoryClient>().Object,days.Object);Evidence=new(Db);}
        public void Dispose()=>Db.Dispose();
    }
    [Fact] public async Task EvidenceRequiresOwnedSkillAndSourceAndDeduplicatesRetries()
    {
        using var f=new Fixture();var skill=await f.Records.SaveSkillAsync(f.User,null,new(){Name="C++",LearningStatus="learning"},default);
        var work=new WorkExperience{UserId=f.User,Company="Owned",Title="Engineer"};var foreign=new Project{UserId=Guid.NewGuid(),Name="Private project"};f.Db.WorkExperiences.Add(work);f.Db.Projects.Add(foreign);await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Evidence.AddAsync(f.User,skill!.Id,new("project",foreign.Id,"Forbidden"),default));Assert.Empty(f.Db.SkillEvidence);
        Assert.Null(await f.Evidence.ListAsync(Guid.NewGuid(),skill!.Id,default));Assert.Null(await f.Evidence.AddAsync(Guid.NewGuid(),skill.Id,new("work",work.Id,"Foreign"),default));
        var first=await f.Evidence.AddAsync(f.User,skill.Id,new("work",work.Id,"Built a device"),default);var retry=await f.Evidence.AddAsync(f.User,skill.Id,new("work",work.Id,"Different retry"),default);Assert.Equal(first!.Id,retry!.Id);Assert.Equal("Built a device",retry.Notes);Assert.Single(f.Db.SkillEvidence);
        Assert.False(await f.Evidence.RemoveAsync(Guid.NewGuid(),skill.Id,first.Id,default));f.Db.WorkExperiences.Remove(work);await f.Db.SaveChangesAsync();var unavailable=(await f.Evidence.ListAsync(f.User,skill.Id,default))!.Single();Assert.False(unavailable.Available);Assert.Null(unavailable.Url);Assert.Equal("Built a device",unavailable.Notes);
        Assert.True(await f.Evidence.RemoveAsync(f.User,skill.Id,first.Id,default));Assert.Empty(f.Db.SkillEvidence);
    }
    [Fact] public async Task StructuredRecordsAndResumeInclusionPreserveLegacyDataAndLearningLabels()
    {
        using var f=new Fixture();var legacy=await f.Records.SaveWorkAsync(f.User,null,new(){Company="Legacy",Title="Engineer",Bullets="Original bullet"},default);Assert.True(legacy!.IncludeInResume);Assert.Empty(legacy.Accomplishments!);
        var structured=await f.Records.SaveWorkAsync(f.User,null,new(){Company="Structured",Title="Developer",Accomplishments=[new("Built API","Reduced delays","20% measured reduction")]},default);Assert.Contains("Built API",structured!.Bullets);Assert.Contains("20%",structured.Bullets);
        await f.Records.SaveWorkAsync(f.User,legacy.Id,new(){Company="Legacy",Title="Engineer",Bullets="Original bullet",IncludeInResume=false},default);
        var project=await f.Records.SaveProjectAsync(f.User,null,new(){Name="Device",Description="Overview",Accomplishments=[new("Wrote firmware","Added checks",null)],IncludeInResume=false},default);
        await f.Records.SaveProjectAsync(f.User,project!.Id,new(){Name="Device",Description="Edited overview"},default);Assert.False(f.Db.Projects.Single().IncludeInResume);Assert.Contains("firmware",f.Db.Projects.Single().AccomplishmentsJson);
        await f.Records.SaveSkillAsync(f.User,null,new(){Name="Rust",LearningStatus="learning"},default);var resume=await new ResumeDataBuilder(f.Db).BuildAsync(f.User);Assert.Single(resume.Experience);Assert.Equal(structured.Id,resume.Experience[0].Id);Assert.Empty(resume.Projects);Assert.Contains("Rust (learning)",resume.Skills.SelectMany(x=>x.Skills));Assert.Equal("Original bullet",f.Db.WorkExperiences.Single(x=>x.Id==legacy.Id).Bullets);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Records.SaveWorkAsync(f.User,structured.Id,new(){Company="Structured",Title="Developer",Bullets="Overwrite structured content"},default));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Records.SaveProjectAsync(f.User,project.Id,new(){Name="Device",Accomplishments=[new("",null,null)]},default));
    }
    [Fact] public async Task SkillStatusIsValidatedAndOmissionDoesNotResetIt()
    {
        using var f=new Fixture();var skill=await f.Records.SaveSkillAsync(f.User,null,new(){Name="Python",LearningStatus="demonstrated"},default);await f.Records.SaveSkillAsync(f.User,skill!.Id,new(){Name="Python"},default);Assert.Equal("demonstrated",f.Db.Skills.Single().LearningStatus);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Records.SaveSkillAsync(f.User,null,new(){Name="Bad",LearningStatus="certified-by-AI"},default));Assert.Single(f.Db.Skills);
    }
    [Fact] public async Task CertificateRemindersRespectDatesDeduplicationDismissalAndPreferences()
    {
        using var f=new Fixture();var reminders=new ReminderService(f.Db);var day=LocalDay.At(DateTimeOffset.UtcNow,"America/Los_Angeles");var cert=await f.Records.SaveCertificateAsync(f.User,null,new(){Name="Cloud",Expiry=day.Date.AddDays(10).ToString("yyyy-MM-dd")},default);var plan=new DailyPlanDto{Date=day.Date.ToString("yyyy-MM-dd"),TimeZone=day.Zone.Id};
        await reminders.RemindAsync(f.User,plan,default);await reminders.RemindAsync(f.User,plan,default);Assert.Single(f.Db.Notifications);var n=f.Db.Notifications.Single();Assert.Contains("expiring",n.Title);Assert.Equal("/skills?tab=certificates",n.ActionUrl);
        await reminders.ArchiveAsync(f.User,n.Id,default);await reminders.RemindAsync(f.User,plan,default);Assert.Empty(await reminders.ListAsync(f.User,false,1,default));Assert.Single(f.Db.Notifications);
        await f.Records.SaveCertificateAsync(f.User,cert!.Id,new(){Name="Cloud",Expiry=day.Date.AddDays(-1).ToString("yyyy-MM-dd")},default);await reminders.RemindAsync(f.User,plan,default);Assert.Contains("expired",(await reminders.ListAsync(f.User,false,1,default)).Single().Title);
        await reminders.SavePreferencesAsync(f.User,new(true,true,true,30,false),default);await reminders.RemindAsync(f.User,plan,default);Assert.Empty(await reminders.ListAsync(f.User,false,1,default));
        await reminders.SavePreferencesAsync(f.User,new(true,true,true,30),default);Assert.False((await reminders.PreferencesAsync(f.User,default)).CertificateExpiryEnabled);Assert.Empty(await reminders.ListAsync(Guid.NewGuid(),false,1,default));
    }
    [Fact] public async Task ApiLinksEvidenceAndResumeDataHonorsRecordChoices()
    {
        using var app=new HabitApiTests.Factory();using var client=app.CreateClient();var skill=await (await client.PostAsJsonAsync("/api/skills",new{name="C++",learningStatus="learning"})).Content.ReadFromJsonAsync<SkillDto>();
        var work=await (await client.PostAsJsonAsync("/api/experience/work",new{company="Private resume record",title="Engineer",includeInResume=false,accomplishments=new[]{new{action="Built service",outcome="Improved reliability",metric="Measured 20%"}}})).Content.ReadFromJsonAsync<WorkExperienceDto>();
        var response=await client.PostAsJsonAsync($"/api/skills/{skill!.Id}/evidence",new{sourceType="work",sourceId=work!.Id,notes="Synthetic proof"});Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        response=await client.PostAsJsonAsync($"/api/skills/{skill.Id}/evidence",new{sourceType="course",sourceId=Guid.NewGuid()});Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);
        var data=await client.GetFromJsonAsync<LifeOS.Application.DTOs.Documents.ResumeDataDto>("/api/documents/resume-data");Assert.Empty(data!.Experience);Assert.Contains("C++ (learning)",data.Skills.SelectMany(g=>g.Skills));Assert.Single((await client.GetFromJsonAsync<List<SkillEvidenceDto>>($"/api/skills/{skill.Id}/evidence"))!);
    }
}
