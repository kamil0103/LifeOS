using LifeOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Institution> Institutions => Set<Institution>();
    public DbSet<Degree> Degrees => Set<Degree>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<WorkExperience> WorkExperiences => Set<WorkExperience>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<Certificate> Certificates => Set<Certificate>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobApplication> JobApplications => Set<JobApplication>();
    public DbSet<ApplicationStatusHistory> ApplicationStatusHistories => Set<ApplicationStatusHistory>();
    public DbSet<CompanyNote> CompanyNotes => Set<CompanyNote>();
    public DbSet<ResumeVersion> ResumeVersions => Set<ResumeVersion>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Habit> Habits => Set<Habit>();
    public DbSet<HabitCompletion> HabitCompletions => Set<HabitCompletion>();
    public DbSet<Streak> Streaks => Set<Streak>();
    public DbSet<XpTransaction> XpTransactions => Set<XpTransaction>();
    public DbSet<DailyMission> DailyMissions => Set<DailyMission>();
    public DbSet<AiCoachMessage> AiCoachMessages => Set<AiCoachMessage>();
    public DbSet<CodingProblem> CodingProblems => Set<CodingProblem>();
    public DbSet<ProblemAttempt> ProblemAttempts => Set<ProblemAttempt>();
    public DbSet<BibleBook> BibleBooks => Set<BibleBook>();
    public DbSet<BibleVerse> BibleVerses => Set<BibleVerse>();
    public DbSet<Bookmark> Bookmarks => Set<Bookmark>();
    public DbSet<ReadingPlan> ReadingPlans => Set<ReadingPlan>();
    public DbSet<ReadingPlanDay> ReadingPlanDays => Set<ReadingPlanDay>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Username).IsUnique();
            e.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<UserProfile>(e =>
        {
            e.HasKey(p => p.UserId);
            e.HasOne(p => p.User).WithOne(u => u.Profile).HasForeignKey<UserProfile>(p => p.UserId);
        });

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.HasOne(rt => rt.User).WithMany().HasForeignKey(rt => rt.UserId);
        });

        modelBuilder.Entity<Institution>(e =>
        {
            e.HasMany(i => i.Degrees).WithOne(d => d.Institution).HasForeignKey(d => d.InstitutionId);
            e.HasMany(i => i.UnassignedCourses).WithOne(c => c.Institution).HasForeignKey(c => c.InstitutionId);
        });

        modelBuilder.Entity<Degree>(e =>
        {
            e.HasMany(d => d.Courses).WithOne(c => c.Degree).HasForeignKey(c => c.DegreeId);
        });

        modelBuilder.Entity<Job>(e =>
        {
            e.HasMany(j => j.Applications).WithOne(a => a.Job).HasForeignKey(a => a.JobId);
        });

        modelBuilder.Entity<JobApplication>(e =>
        {
            e.HasMany(a => a.StatusHistory).WithOne(h => h.Application).HasForeignKey(h => h.ApplicationId);
        });

        modelBuilder.Entity<Habit>(e =>
        {
            e.HasMany(h => h.Completions).WithOne(c => c.Habit).HasForeignKey(c => c.HabitId);
            e.HasOne(h => h.Streak).WithOne(s => s.Habit).HasForeignKey<Streak>(s => s.HabitId);
        });

        modelBuilder.Entity<DailyMission>(e =>
        {
            e.HasIndex(dm => new { dm.UserId, dm.MissionDate }).IsUnique();
        });

        modelBuilder.Entity<CodingProblem>(e =>
        {
            e.HasMany(p => p.Attempts).WithOne(a => a.Problem).HasForeignKey(a => a.ProblemId);
        });

        modelBuilder.Entity<BibleBook>(e =>
        {
            e.HasIndex(b => b.BookOrder).IsUnique();
            e.HasMany(b => b.Verses).WithOne(v => v.Book).HasForeignKey(v => v.BookId);
        });

        modelBuilder.Entity<BibleVerse>(e =>
        {
            e.HasIndex(v => new { v.BookId, v.Chapter, v.VerseNumber }).IsUnique();
        });

        modelBuilder.Entity<Bookmark>(e =>
        {
            e.HasIndex(b => new { b.UserId, b.VerseId }).IsUnique();
            e.HasOne(b => b.Verse).WithMany().HasForeignKey(b => b.VerseId);
        });

        modelBuilder.Entity<ReadingPlan>(e =>
        {
            e.HasMany(p => p.Days).WithOne(d => d.Plan).HasForeignKey(d => d.PlanId);
        });

        modelBuilder.Entity<ReadingPlanDay>(e =>
        {
            e.HasOne(d => d.Book).WithMany().HasForeignKey(d => d.BookId);
        });
    }
}
