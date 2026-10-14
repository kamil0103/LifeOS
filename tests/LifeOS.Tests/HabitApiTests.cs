using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using LifeOS.Application.Interfaces;
using LifeOS.Application.Services;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
namespace LifeOS.Tests;

public class HabitApiTests
{
    [Fact]
    public async Task ForeignHabitHistoryIsNotAccessible()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        Guid habitId;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var habit = new Habit { UserId = Guid.NewGuid(), Name = "Private habit" };
            db.Habits.Add(habit);
            db.HabitCompletions.Add(new HabitCompletion { HabitId = habit.Id, CompletedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
            habitId = habit.Id;
        }
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/habits/{habitId}/completions")).StatusCode);
    }
    [Fact]
    public async Task TodayEndpointIncludesOnlyLocalDayCompletions()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var habit = new Habit { UserId = TestAuth.UserId, Name = "Today", IsActive = true };
            db.Habits.Add(habit);
            db.HabitCompletions.Add(new HabitCompletion { HabitId = habit.Id, CompletedAt = Factory.Day.Start.AddHours(1) });
            await db.SaveChangesAsync();
        }
        var result = await client.GetAsync("/api/habits/today");
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Contains("\"isCompleted\":true", await result.Content.ReadAsStringAsync());
    }
    private class Factory : WebApplicationFactory<Program>
    {
        public static LocalDay Day = LocalDay.At(DateTimeOffset.Parse("2026-10-15T01:00:00Z"), "America/Los_Angeles");
        private readonly string databaseName = Guid.NewGuid().ToString();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Secret"] = "test-only-secret-with-at-least-thirty-two-characters" }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<AppDbContext>();
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<AppDbContext>>();
                services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName));
                var days = new Mock<ILocalDayService>();
                days.Setup(x => x.TodayAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(Day);
                services.RemoveAll<ILocalDayService>();
                services.AddSingleton(days.Object);
                services.AddAuthentication(options => { options.DefaultAuthenticateScheme = "Test"; options.DefaultChallengeScheme = "Test"; }).AddScheme<AuthenticationSchemeOptions, TestAuth>("Test", _ => { });
            });
        }
    }
    private class TestAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public static readonly Guid UserId = Guid.NewGuid();
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, UserId.ToString()) }, "Test")), "Test")));
    }
}
