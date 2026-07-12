using LifeOS.Application.DTOs.Documents;
using LifeOS.Application.Interfaces;
using LifeOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Infrastructure.Services;

public class ResumeDataBuilder : IResumeDataBuilder
{
    private readonly AppDbContext _context;

    public ResumeDataBuilder(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ResumeDataDto> BuildAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = await _context.UserProfiles
            .AsNoTracking()
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        var degrees = await _context.Degrees
            .AsNoTracking()
            .Include(d => d.Institution)
            .Where(d => d.UserId == userId)
            .ToListAsync(ct);

        var courses = await _context.Courses
            .AsNoTracking()
            .Where(c => c.UserId == userId && c.IsMajorRelated)
            .ToListAsync(ct);

        var workExp = await _context.WorkExperiences
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.StartDate)
            .ToListAsync(ct);

        var projects = await _context.Projects
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync(ct);

        var skills = await _context.Skills
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderBy(s => s.Category)
            .ThenBy(s => s.Name)
            .ToListAsync(ct);

        var certs = await _context.Certificates
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.DateObtained)
            .ToListAsync(ct);

        var skillsByCat = skills
            .GroupBy(s => s.Category ?? "Other")
            .Select(g => new ResumeSkillGroupDto
            {
                Category = g.Key,
                Skills = g.Select(s => s.Name).ToList()
            })
            .ToList();

        return new ResumeDataDto
        {
            Title = "My Resume",
            Template = "modern",
            SectionOrder = new List<string> { "education", "experience", "skills", "projects", "certifications" },
            Profile = new ResumeProfileDto
            {
                FullName = profile?.FullName ?? "",
                Email = profile?.User?.Email ?? "",
                Phone = profile?.Phone ?? "",
                Location = profile?.Location ?? "",
                LinkedIn = profile?.LinkedInUrl ?? "",
                Portfolio = profile?.PortfolioUrl ?? "",
                GitHub = profile?.GitHubUrl ?? "",
                Summary = profile?.Summary ?? ""
            },
            Education = degrees.Select(d => new ResumeEducationDto
            {
                Id = d.Id,
                School = d.Institution?.Name ?? "",
                Degree = d.DegreeName,
                Field = d.Field ?? "",
                GraduationDate = d.EndDate,
                Gpa = d.Gpa,
                Honors = d.Honors,
                IsCurrent = d.IsCurrent,
                StartDate = d.StartDate,
                EndDate = d.EndDate
            }).ToList(),
            Experience = workExp.Select(e => new ResumeExperienceDto
            {
                Id = e.Id,
                Title = e.Title,
                Company = e.Company,
                Location = e.Location ?? "",
                StartDate = e.StartDate,
                EndDate = e.EndDate,
                IsCurrent = e.IsCurrent,
                Bullets = e.Bullets ?? ""
            }).ToList(),
            Skills = skillsByCat,
            Projects = projects.Select(p => new ResumeProjectDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description ?? "",
                Technologies = p.Technologies ?? "",
                Link = p.Link ?? "",
                StartDate = p.StartDate,
                EndDate = p.EndDate,
                IsCurrent = p.IsCurrent
            }).ToList(),
            Certifications = certs.Select(c => new ResumeCertificationDto
            {
                Name = c.Name,
                Organization = c.Issuer ?? "",
                Date = c.DateObtained
            }).ToList(),
            Courses = courses.Select(c => new ResumeCourseDto
            {
                Code = c.Code ?? "",
                Name = c.Name,
                Grade = c.Grade,
                Credits = c.Credits?.ToString(),
                Term = c.Term,
                Description = c.Description
            }).ToList()
        };
    }
}
