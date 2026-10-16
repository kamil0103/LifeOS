using System.Net;
using System.Net.Http.Json;
using LifeOS.Application.DTOs.Documents;
using LifeOS.Application.Interfaces;
using LifeOS.Infrastructure.Data;
using LifeOS.Infrastructure.Resumes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Hosting;
using Moq;
using Xunit;
namespace LifeOS.Tests;
public class ResumeDraftTests
{
    private static AppDbContext Database() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    [Fact]
    public async Task DraftRestorationIsOwnedAndSeparateFromNamedVersions()
    {
        using var db=Database();var pdf=new Mock<IResumeGenerator>();var s=new ResumeDraftService(db,pdf.Object);var user=Guid.NewGuid();
        Assert.Null(await s.GetAsync(user,default));var first=await s.SaveAsync(user,new(){Data=new(){Title="My draft",Profile=new(){FullName="Test Candidate"}}},default);
        Assert.Equal("My draft",(await s.GetAsync(user,default))!.Data.Title);Assert.Null(await s.GetAsync(Guid.NewGuid(),default));Assert.Empty(db.ResumeVersions);Assert.Empty(db.Documents);
        var retry=await s.SaveAsync(user,new(){Data=first.Data},default);Assert.Equal(first.Revision,retry.Revision);
        var updated=await s.SaveAsync(user,new(){ExpectedRevision=first.Revision,Data=new(){Title="Changed"}},default);Assert.NotEqual(first.Revision,updated.Revision);Assert.Single(db.ResumeDrafts);
        await Assert.ThrowsAsync<RecordConflictException>(()=>s.SaveAsync(user,new(){ExpectedRevision=first.Revision,Data=new(){Title="Stale overwrite"}},default));Assert.Equal("Changed",(await s.GetAsync(user,default))!.Data.Title);
        await Assert.ThrowsAsync<RecordConflictException>(()=>s.SaveAsync(Guid.NewGuid(),new(){ExpectedRevision=updated.Revision,Data=new(){Title="Foreign revision"}},default));
    }
    [Fact]
    public async Task PreviewRendersWithoutSavingDocumentsAndValidatesPayload()
    {
        using var db=Database();var pdf=new Mock<IResumeGenerator>();pdf.Setup(p=>p.GenerateResumePdfAsync(It.IsAny<ResumeDataDto>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync(new byte[]{37,80,68,70});var s=new ResumeDraftService(db,pdf.Object);
        var result=await s.PreviewAsync(new(){Template="",SectionOrder=[]},default);Assert.Equal(4,result.Length);Assert.Empty(db.Documents);Assert.Empty(db.ResumeVersions);Assert.Empty(db.ResumeDrafts);
        pdf.Verify(p=>p.GenerateResumePdfAsync(It.Is<ResumeDataDto>(d=>d.SectionOrder.Contains("education")),"harvard",It.IsAny<CancellationToken>()),Times.Once);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>s.SaveAsync(Guid.NewGuid(),new(){Data=new(){Title=new string('x',201)}},default));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>s.PreviewAsync(new(){Profile=new(){Summary=new string('x',512001)}},default));Assert.Empty(db.ResumeDrafts);
    }
    [Fact]
    public async Task ApiRestoresDraftRejectsStaleVersionsAndServesUnstoredPdf()
    {
        using var app=new Factory();using var client=app.CreateClient();Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync("/api/resume-draft")).StatusCode);
        var response=await client.PutAsJsonAsync("/api/resume-draft",new{data=new{title="Draft",profile=new{fullName="Candidate"}}});response.EnsureSuccessStatusCode();var draft=(await response.Content.ReadFromJsonAsync<ResumeDraftDto>())!;
        Assert.Equal(HttpStatusCode.Conflict,(await client.PutAsJsonAsync("/api/resume-draft",new{data=new{title="Other tab"}})).StatusCode);
        var updated=await client.PutAsJsonAsync("/api/resume-draft",new{expectedRevision=draft.Revision,data=new{title="Accepted"}});updated.EnsureSuccessStatusCode();
        var pdf=await client.PostAsJsonAsync("/api/resume-draft/preview",new{data=new{title="Preview only"}});pdf.EnsureSuccessStatusCode();Assert.Equal("application/pdf",pdf.Content.Headers.ContentType!.MediaType);Assert.True(pdf.Headers.CacheControl!.NoStore);
        using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();Assert.Empty(db.Documents);Assert.Empty(db.ResumeVersions);Assert.Single(db.ResumeDrafts);
    }
    private sealed class Factory:HabitApiTests.Factory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder){base.ConfigureWebHost(builder);builder.ConfigureServices(services=>{services.RemoveAll<IResumeGenerator>();var pdf=new Mock<IResumeGenerator>();pdf.Setup(p=>p.GenerateResumePdfAsync(It.IsAny<ResumeDataDto>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync(new byte[]{37,80,68,70});services.AddSingleton(pdf.Object);});}
    }
}
