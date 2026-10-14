using System.Net;
using System.Net.Http.Json;
using LifeOS.Application.DTOs.Jobs;
using LifeOS.Application.Interfaces;
using LifeOS.Application.Services;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Career;
using LifeOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
namespace LifeOS.Tests;
public class CareerTests
{
    [Fact]
    public void JobUrlsIgnoreTrackingButKeepRequisitionIdsAndFragments()
    {
        Assert.Equal(JobIdentity.CanonicalUrl("https://example.com/jobs/1?jobId=3"),JobIdentity.CanonicalUrl("https://EXAMPLE.com/jobs/1/?utm_source=feed&jobId=3&gclid=test"));
        Assert.NotEqual(JobIdentity.CanonicalUrl("https://example.com/jobs?jobId=1"),JobIdentity.CanonicalUrl("https://example.com/jobs?jobId=2"));
        Assert.NotEqual(JobIdentity.CanonicalUrl("https://example.com/#job1"),JobIdentity.CanonicalUrl("https://example.com/#job2"));
        Assert.Throws<InvalidOperationException>(()=>JobIdentity.CanonicalUrl("javascript:alert(1)"));Assert.Throws<InvalidOperationException>(()=>JobIdentity.CanonicalUrl("https://user:secret@example.com/jobs"));
    }
    [Fact]
    public async Task JobSavesReuseOwnedMatchesAndKeepDifferentPostings()
    {
        using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);var service=new CareerWorkflowService(db);var user=Guid.NewGuid();
        var first=await service.SaveJobAsync(user,new(){Title="Engineer",Company="Example",Url="https://example.com/jobs?id=1"},default);
        var repeat=await service.SaveJobAsync(user,new(){Title="Engineer",Company="Example",Url="https://example.com/jobs?id=1&utm_source=rss"},default);Assert.Equal(first.Id,repeat.Id);
        var other=await service.SaveJobAsync(user,new(){Title="Engineer",Company="Example",Url="https://example.com/jobs?id=2"},default);Assert.NotEqual(first.Id,other.Id);
        var foreign=await service.SaveJobAsync(Guid.NewGuid(),new(){Title="Engineer",Company="Example",Url="https://example.com/jobs?id=1"},default);Assert.NotEqual(first.Id,foreign.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.SaveJobAsync(user,new(){Title=" ",Company="Example"},default));
    }
    [Fact]
    public async Task TrackedApplicationsReuseTheOriginalResumeAndProtectLinkedAssets()
    {
        using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);var service=new CareerWorkflowService(db);var user=Guid.NewGuid();
        var job=await service.SaveJobAsync(user,new(){Title="Role",Company="Example"},default);var resume=new ResumeVersion{UserId=user,Title="Original resume"};db.ResumeVersions.Add(resume);await db.SaveChangesAsync();
        var first=await service.TrackApplicationAsync(user,new(){JobId=job.Id,ResumeVersionId=resume.Id,Notes="Original notes"},default);var again=await service.TrackApplicationAsync(user,new(){JobId=job.Id,Notes="Retry"},default);
        Assert.Equal(first.Id,again.Id);Assert.Equal(resume.Id,again.ResumeVersionId);Assert.Equal("Original notes",again.Notes);Assert.Single(db.JobApplications);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.TrackApplicationAsync(Guid.NewGuid(),new(){JobId=job.Id},default));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.UpdateJobAsync(user,job.Id,new(){Title="Role",Company="Example",Status="offer"},default));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.EnsureAssetUnusedAsync(resume.Id,true,default));
    }
    [Fact]
    public async Task ApplicationApiAndProtectedDeletesRespectOwnership()
    {
        using var app=new HabitApiTests.Factory();using var client=app.CreateClient();Guid resumeId,documentId,jobId;
        using(var scope=app.Services.CreateScope()){
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var user=HabitApiTests.TestAuth.UserId;var resume=new ResumeVersion{UserId=user,Title="Owned"};var doc=new Document{UserId=user,Type="cover_letter",Filename="synthetic.pdf",StoragePath="synthetic-path"};var job=new Job{UserId=user,Title="Role",Company="Example"};db.AddRange(resume,doc,job);await db.SaveChangesAsync();resumeId=resume.Id;documentId=doc.Id;jobId=job.Id;
        }
        var created=await client.PostAsJsonAsync("/api/job-applications",new{jobId,resumeVersionId=resumeId});Assert.Equal(HttpStatusCode.OK,created.StatusCode);var body=await created.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();var id=body.GetProperty("id").GetGuid();
        var retry=await client.PostAsJsonAsync("/api/job-applications",new{jobId});var duplicate=await retry.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();Assert.Equal(id,duplicate.GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.NoContent,(await client.PutAsJsonAsync($"/api/planning/applications/{id}/details",new{resumeVersionId=resumeId,coverLetterDocumentId=documentId})).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.DeleteAsync($"/api/resume-versions/{resumeId}")).StatusCode);Assert.Equal(HttpStatusCode.BadRequest,(await client.DeleteAsync($"/api/documents/{documentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.DeleteAsync($"/api/jobs/{jobId}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/companynotes",new{companyName="Example",rating=6})).StatusCode);
    }
    [Fact]
    public async Task JobControllerDelegatesToCareerService()
    {
        var workflow=new Mock<ICareerWorkflowService>();var user=HabitApiTests.TestAuth.UserId;var request=new CreateJobRequest{Title="Role",Company="Example"};workflow.Setup(x=>x.SaveJobAsync(user,request,It.IsAny<CancellationToken>())).ReturnsAsync(new JobDto{Title="Role"});
        using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var controller=new LifeOS.Api.Controllers.JobsController(db,Mock.Of<IAiProvider>(),Mock.Of<IHttpClientFactory>(),Mock.Of<Microsoft.Extensions.Configuration.IConfiguration>(),workflow.Object){ControllerContext=new(){HttpContext=new Microsoft.AspNetCore.Http.DefaultHttpContext{User=new(new System.Security.Claims.ClaimsIdentity([new(System.Security.Claims.ClaimTypes.NameIdentifier,user.ToString())],"test"))}}};
        await controller.CreateJob(request,default);workflow.Verify(x=>x.SaveJobAsync(user,request,It.IsAny<CancellationToken>()),Times.Once);
    }
}
