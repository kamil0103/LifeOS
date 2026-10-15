using System.Net;
using System.Net.Http.Json;
using LifeOS.Application.DTOs.Education;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using LifeOS.Infrastructure.Education;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace LifeOS.Tests;
public class EducationRecordTests
{
    [Fact]
    public async Task ParentOwnershipAndAlignmentAreRequired()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var user = Guid.NewGuid(); var other = Guid.NewGuid(); var school = new Institution { UserId = user, Name = "My school" }; var foreign = new Institution { UserId = other, Name = "Private school" };
        db.Institutions.AddRange(school, foreign); await db.SaveChangesAsync(); var service = new EducationRecordService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveDegreeAsync(user, null, new() { DegreeName = "Invalid", InstitutionId = foreign.Id }, default));
        var degree = await service.SaveDegreeAsync(user, null, new() { DegreeName = "CS", InstitutionId = school.Id }, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveCourseAsync(other, null, new() { Name = "Invalid", DegreeId = degree!.Id }, default));
        var course = await service.SaveCourseAsync(user, null, new() { Name = "Algorithms", DegreeId = degree!.Id, Credits = 3 }, default); Assert.Equal(school.Id, course!.InstitutionId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveCourseAsync(user, null, new() { Name = "Bad credits", InstitutionId = school.Id, Credits = -1 }, default));
        Assert.Null(await service.SaveCourseAsync(other, course.Id, new() { Name = "Foreign edit" }, default)); Assert.Single(db.Courses);
    }
    [Fact]
    public async Task MovingAndDeletingDegreeKeepsCoursesAtTheRightSchool()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var user = Guid.NewGuid(); var a = new Institution { UserId = user, Name = "A" }; var b = new Institution { UserId = user, Name = "B" }; db.Institutions.AddRange(a,b); await db.SaveChangesAsync(); var service = new EducationRecordService(db);
        var degree = await service.SaveDegreeAsync(user, null, new() { DegreeName = "CS", InstitutionId = a.Id }, default); var course = await service.SaveCourseAsync(user, null, new() { Name = "Algorithms", DegreeId = degree!.Id }, default);
        await service.SaveDegreeAsync(user, degree!.Id, new() { DegreeName = "CS", InstitutionId = b.Id, IsCurrent = true, EndDate = "2028" }, default); Assert.Equal(b.Id, db.Courses.Single().InstitutionId); Assert.Null(db.Degrees.Single().EndDate);
        Assert.True(await service.DeleteDegreeAsync(user, degree.Id, default)); Assert.Empty(db.Degrees); Assert.Single(db.Courses); Assert.Null(db.Courses.Single().DegreeId); Assert.Equal(b.Id, db.Courses.Single().InstitutionId);
    }
    [Fact]
    public async Task ApiRejectsForeignParentsAndDeletionMakesCoursesUnassigned()
    {
        using var app = new HabitApiTests.Factory(); using var client = app.CreateClient(); Guid foreign;
        using (var scope = app.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var institution = new Institution { UserId = Guid.NewGuid(), Name = "Foreign institution" }; db.Institutions.Add(institution); await db.SaveChangesAsync(); foreign = institution.Id; }
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/education/degrees", new { degreeName = "Invalid", institutionId = foreign })).StatusCode);
        var schoolResponse = await client.PostAsJsonAsync("/api/education/institutions", new { name = "Owned school" }); var school = await schoolResponse.Content.ReadFromJsonAsync<InstitutionDto>();
        var response = await client.PostAsJsonAsync("/api/education/degrees", new { degreeName = "CS", institutionId = school!.Id }); var degree = await response.Content.ReadFromJsonAsync<DegreeDto>();
        response = await client.PostAsJsonAsync("/api/education/courses", new { name = "Algorithms", degreeId = degree!.Id }); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var institutions = await client.GetFromJsonAsync<List<InstitutionDto>>("/api/education/institutions"); Assert.Single(institutions!); Assert.Empty(institutions![0].UnassignedCourses); Assert.Single(institutions[0].Degrees[0].Courses);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/education/degrees/{degree.Id}")).StatusCode);
        institutions = await client.GetFromJsonAsync<List<InstitutionDto>>("/api/education/institutions"); Assert.Single(institutions![0].UnassignedCourses); Assert.Empty(institutions[0].Degrees);
    }
}
