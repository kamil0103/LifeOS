using LifeOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ResumeDraft> ResumeDrafts => Set<ResumeDraft>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Institution> Institutions => Set<Institution>();
    public DbSet<Degree> Degrees => Set<Degree>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<WorkExperience> WorkExperiences => Set<WorkExperience>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<SkillEvidence> SkillEvidence => Set<SkillEvidence>();
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
    public DbSet<ApplicationInterview> ApplicationInterviews => Set<ApplicationInterview>();
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();
    public DbSet<ReminderPreferences> ReminderPreferences => Set<ReminderPreferences>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalSettings> JournalSettings => Set<JournalSettings>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<RecipeInstructionStep> RecipeInstructionSteps => Set<RecipeInstructionStep>();
    public DbSet<RecipeSettings> RecipeSettings => Set<RecipeSettings>();

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

        modelBuilder.Entity<Recipe>(e =>
        {
            e.HasMany(r => r.Ingredients).WithOne(i => i.Recipe).HasForeignKey(i => i.RecipeId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(r => r.Steps).WithOne(s => s.Recipe).HasForeignKey(s => s.RecipeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Job>(e =>
        {
            e.HasMany(j => j.Applications).WithOne(a => a.Job).HasForeignKey(a => a.JobId);
        });

        modelBuilder.Entity<JobApplication>(e =>
        {
            e.HasMany(a => a.StatusHistory).WithOne(h => h.Application).HasForeignKey(h => h.ApplicationId);
        });

        modelBuilder.Entity<Skill>().Property(s => s.LearningStatus).HasDefaultValue("unspecified");
        modelBuilder.Entity<WorkExperience>().Property(w => w.IncludeInResume).HasDefaultValue(true);
        modelBuilder.Entity<WorkExperience>().Property(w => w.AccomplishmentsJson).HasDefaultValue("[]");
        modelBuilder.Entity<Project>().Property(p => p.IncludeInResume).HasDefaultValue(true);
        modelBuilder.Entity<Project>().Property(p => p.AccomplishmentsJson).HasDefaultValue("[]");
        modelBuilder.Entity<ResumeDraft>(e => { e.HasKey(d => d.UserId); e.HasOne(d => d.User).WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade); });
        modelBuilder.Entity<ReminderPreferences>().Property(p => p.CertificateExpiryEnabled).HasDefaultValue(true);
        modelBuilder.Entity<SkillEvidence>(e => { e.HasIndex(x => new { x.SkillId, x.SourceType, x.SourceId }).IsUnique(); e.HasOne(x => x.Skill).WithMany().HasForeignKey(x => x.SkillId).OnDelete(DeleteBehavior.Cascade); e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade); });
        modelBuilder.Entity<Habit>().Property(h => h.ScheduleDaysMask).HasDefaultValue(127);
        modelBuilder.Entity<HabitCompletion>().Property(c => c.IsCompleted).HasDefaultValue(true);
        modelBuilder.Entity<Habit>(e =>
        {
            e.HasMany(h => h.Completions).WithOne(c => c.Habit).HasForeignKey(c => c.HabitId);
            e.HasOne(h => h.Streak).WithOne(s => s.Habit).HasForeignKey<Streak>(s => s.HabitId);
        });

        modelBuilder.Entity<ReminderPreferences>(e => { e.HasKey(p => p.UserId); e.HasOne(p => p.User).WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade); });
        modelBuilder.Entity<Notification>(e => e.HasIndex(n => new { n.UserId, n.SourceKey }).IsUnique());
        modelBuilder.Entity<ApplicationInterview>(e => { e.HasIndex(i => new { i.UserId, i.RequestId }).IsUnique(); e.HasOne(i => i.Application).WithMany().HasForeignKey(i => i.ApplicationId).OnDelete(DeleteBehavior.Cascade); e.HasOne(i => i.User).WithMany().HasForeignKey(i => i.UserId).OnDelete(DeleteBehavior.Cascade); });
        modelBuilder.Entity<CalendarEvent>(e => e.HasIndex(c => new { c.UserId, c.ActionKey }).IsUnique());
        modelBuilder.Entity<CalendarEvent>(e => e.HasOne(c => c.Application).WithMany().HasForeignKey(c => c.ApplicationId).OnDelete(DeleteBehavior.SetNull));

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

        modelBuilder.Entity<CalendarEvent>(e =>
        {
            e.HasIndex(ce => new { ce.UserId, ce.StartTime });
            e.HasOne(ce => ce.Habit).WithMany().HasForeignKey(ce => ce.HabitId);
            e.HasOne(ce => ce.Job).WithMany().HasForeignKey(ce => ce.JobId);
        });

        modelBuilder.Entity<Notification>(e =>
        {
            e.HasIndex(n => new { n.UserId, n.IsRead });
        });
    }
}
