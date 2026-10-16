using System.Net;
using System.Net.Http.Json;
using LifeOS.Application.DTOs.Documents;
using LifeOS.Application.Interfaces;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Career;
using LifeOS.Infrastructure.Data;
using LifeOS.Infrastructure.Resumes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Hosting;
using Moq;
using Xunit;
namespace LifeOS.Tests;
public class DocumentLibraryTests
{
    private sealed class Fixture : IDisposable
    {
        public readonly AppDbContext Db=new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public readonly Guid User=Guid.NewGuid();public readonly Mock<IDocumentStorage> Storage=new(); public readonly DocumentLibraryService Service;public readonly Document Document;public readonly Job Job;public readonly JobApplication App;public readonly ResumeVersion Version;
        public Fixture(){Job=new(){UserId=User,Title="Engineer",Company="Devices"};App=new(){UserId=User,Job=Job,JobId=Job.Id,Status="interview"};Version=new(){UserId=User,Title="Source resume"};Document=new(){UserId=User,Filename="resume.pdf",Type="resume",StoragePath="synthetic-owned"};Db.Jobs.Add(Job);Db.JobApplications.Add(App);Db.ResumeVersions.Add(Version);Db.Documents.Add(Document);Db.SaveChanges();Storage.Setup(s=>s.LoadAsync("synthetic-owned",It.IsAny<CancellationToken>())).ReturnsAsync("%PDF-synthetic"u8.ToArray());Service=new(Db,Storage.Object);}
        public void Dispose()=>Db.Dispose();
    }
    [Fact]
    public async Task LinksInferTheApplicationsJobAndPreservePdfAndApplicationSelections()
    {
        using var f=new Fixture();var saved=(await f.Service.UpdateLinksAsync(f.User,f.Document.Id,new(){ApplicationId=f.App.Id,SourceResumeVersionId=f.Version.Id,ExpectedUpdatedAt=f.Document.UpdatedAt},default))!;
        Assert.Equal(f.Job.Id,saved.JobId);Assert.Equal("Devices",saved.ApplicationCompany);Assert.Equal("interview",saved.ApplicationStatus);Assert.Equal("Source resume",saved.SourceResumeVersionTitle);Assert.Equal("synthetic-owned",f.Document.StoragePath);Assert.Null(f.App.CoverLetterDocumentId);Assert.Null(f.App.ResumeVersionId);
        var career=new CareerWorkflowService(f.Db);await Assert.ThrowsAsync<InvalidOperationException>(()=>career.EnsureAssetUnusedAsync(f.Version.Id,true,default));
        var cleared=await f.Service.UpdateLinksAsync(f.User,f.Document.Id,new(){ExpectedUpdatedAt=saved.UpdatedAt},default);Assert.Null(cleared!.ApplicationId);Assert.Null(cleared.SourceResumeVersionId);Assert.Null(cleared.JobId);await career.EnsureAssetUnusedAsync(f.Version.Id,true,default);
    }
    [Fact]
    public async Task ForeignTargetsMismatchAndStaleVersionsCannotChangeLinks()
    {
        using var f=new Fixture();var foreign=Guid.NewGuid();var job=new Job{UserId=foreign};var app=new JobApplication{UserId=foreign,Job=job};var version=new ResumeVersion{UserId=foreign};f.Db.JobApplications.Add(app);f.Db.ResumeVersions.Add(version);await f.Db.SaveChangesAsync();
        Assert.Empty(await f.Service.ListAsync(foreign,default));Assert.Null(await f.Service.UpdateLinksAsync(foreign,f.Document.Id,new(),default));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Service.ResolveLinksAsync(f.User,new(){ApplicationId=app.Id},default));await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Service.ResolveLinksAsync(f.User,new(){SourceResumeVersionId=version.Id},default));await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Service.ResolveLinksAsync(f.User,new(){ApplicationId=f.App.Id,JobId=job.Id},default));
        await Assert.ThrowsAsync<RecordConflictException>(()=>f.Service.UpdateLinksAsync(f.User,f.Document.Id,new(){JobId=f.Job.Id,ExpectedUpdatedAt=f.Document.UpdatedAt.AddDays(-1)},default));Assert.Null(f.Document.JobId);
    }
    [Fact]
    public async Task PreviewChecksOwnershipAndPdfHeaderBeforeReturningStoredBytes()
    {
        using var f=new Fixture();Assert.Null(await f.Service.PreviewAsync(Guid.NewGuid(),f.Document.Id,default));f.Storage.Verify(s=>s.LoadAsync(It.IsAny<string>(),It.IsAny<CancellationToken>()),Times.Never);
        Assert.NotEmpty((await f.Service.PreviewAsync(f.User,f.Document.Id,default))!);f.Storage.Setup(s=>s.LoadAsync("synthetic-owned",It.IsAny<CancellationToken>())).ReturnsAsync("<html>unsafe</html>"u8.ToArray());await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Service.PreviewAsync(f.User,f.Document.Id,default));
    }
    [Fact]
    public async Task ApiPreviewAndLinkUpdatesAreOwnedAndRevisionChecked()
    {
        using var app=new Factory();using var client=app.CreateClient();Guid id,foreignId,applicationId,sourceId;
        using(var scope=app.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var job=new Job{UserId=HabitApiTests.TestAuth.UserId,Company="Devices"};var application=new JobApplication{UserId=job.UserId,Job=job};var source=new ResumeVersion{UserId=job.UserId,Title="Source"};var doc=new Document{UserId=job.UserId,Filename="owned.pdf",StoragePath="synthetic",Type="resume"};var foreign=new Document{UserId=Guid.NewGuid(),StoragePath="private"};db.JobApplications.Add(application);db.ResumeVersions.Add(source);db.Documents.AddRange(doc,foreign);await db.SaveChangesAsync();id=doc.Id;foreignId=foreign.Id;applicationId=application.Id;sourceId=source.Id;}
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync($"/api/documents/{foreignId}/preview")).StatusCode);var pdf=await client.GetAsync($"/api/documents/{id}/preview");pdf.EnsureSuccessStatusCode();Assert.True(pdf.Headers.CacheControl!.NoStore);Assert.Equal("application/pdf",pdf.Content.Headers.ContentType!.MediaType);
        var doc=(await client.GetFromJsonAsync<List<GeneratedDocumentDto>>("/api/documents"))!.Single();var response=await client.PutAsJsonAsync($"/api/documents/{id}/links",new{applicationId,sourceResumeVersionId=sourceId,expectedUpdatedAt=doc.UpdatedAt});response.EnsureSuccessStatusCode();Assert.Equal(applicationId,(await response.Content.ReadFromJsonAsync<GeneratedDocumentDto>())!.ApplicationId);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PutAsJsonAsync($"/api/documents/{id}/links",new{expectedUpdatedAt=doc.UpdatedAt})).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.DeleteAsync($"/api/resume-versions/{sourceId}")).StatusCode);
    }
    [Fact]
    public async Task PdfGenerationValidatesLinksBeforeStorageAndSavesTheSourceMetadata()
    {
        using var app=new Factory();using var client=app.CreateClient();Guid source;
        using(var scope=app.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var version=new ResumeVersion{UserId=HabitApiTests.TestAuth.UserId,Title="Original"};db.ResumeVersions.Add(version);await db.SaveChangesAsync();source=version.Id;}
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/documents/resume",new{data=new{title="Test"},links=new{sourceResumeVersionId=Guid.NewGuid()}})).StatusCode);
        var response=await client.PostAsJsonAsync("/api/documents/resume",new{data=new{title="Test"},links=new{sourceResumeVersionId=source}});response.EnsureSuccessStatusCode();var doc=Assert.Single((await client.GetFromJsonAsync<List<GeneratedDocumentDto>>("/api/documents"))!);Assert.Equal(source,doc.SourceResumeVersionId);
        app.Storage.Verify(s=>s.SaveAsync(It.IsAny<byte[]>(),It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>()),Times.Once);
    }
    private sealed class Factory:HabitApiTests.Factory
    {
        public readonly Mock<IDocumentStorage> Storage=new();
        protected override void ConfigureWebHost(IWebHostBuilder builder){base.ConfigureWebHost(builder);builder.ConfigureServices(services=>{services.RemoveAll<IDocumentStorage>();Storage.Setup(s=>s.LoadAsync(It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync("%PDF-test"u8.ToArray());Storage.Setup(s=>s.SaveAsync(It.IsAny<byte[]>(),It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync("synthetic");services.AddSingleton(Storage.Object);services.RemoveAll<IResumeGenerator>();var pdf=new Mock<IResumeGenerator>();pdf.Setup(s=>s.GenerateResumePdfAsync(It.IsAny<ResumeDataDto>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync("%PDF-test"u8.ToArray());services.AddSingleton(pdf.Object);});}
    }
}
