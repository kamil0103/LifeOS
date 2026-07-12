using System.IO;
using LifeOS.Application.DTOs.Documents;
using LifeOS.Application.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace LifeOS.Infrastructure.PDF;

public class QuestPdfResumeGenerator : IResumeGenerator
{
    private static readonly string PrimaryColor = "#2c3e50";
    private static readonly string AccentColor = "#3498db";
    private static readonly string DarkGray = "#555555";

    public Task<byte[]> GenerateResumePdfAsync(ResumeDataDto data, string template, CancellationToken ct = default)
    {
        var document = template.ToLowerInvariant() switch
        {
            "classic" => BuildClassicResume(data),
            "minimal" => BuildMinimalResume(data),
            _ => BuildModernResume(data)
        };

        using var stream = new MemoryStream();
        document.GeneratePdf(stream);
        return Task.FromResult(stream.ToArray());
    }

    public Task<byte[]> GenerateCoverLetterPdfAsync(CoverLetterDataDto data, CancellationToken ct = default)
    {
        var document = BuildCoverLetter(data);
        using var stream = new MemoryStream();
        document.GeneratePdf(stream);
        return Task.FromResult(stream.ToArray());
    }

    // ========================
    // MODERN TEMPLATE
    // ========================
    private IDocument BuildModernResume(ResumeDataDto data)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                page.Content().Column(main =>
                {
                    // Header
                    main.Item().Background(PrimaryColor).Padding(20).Column(header =>
                    {
                        header.Item().Text(data.Profile.FullName).FontSize(24).Bold().FontColor(Colors.White);
                        header.Item().PaddingTop(4).Row(row =>
                        {
                            var parts = new List<string>();
                            if (!string.IsNullOrWhiteSpace(data.Profile.Location)) parts.Add(data.Profile.Location);
                            if (!string.IsNullOrWhiteSpace(data.Profile.Phone)) parts.Add(data.Profile.Phone);
                            if (!string.IsNullOrWhiteSpace(data.Profile.Email)) parts.Add(data.Profile.Email);
                            if (parts.Any())
                                row.AutoItem().Text(string.Join(" | ", parts)).FontSize(10).FontColor(Colors.White);
                        });
                        header.Item().PaddingTop(2).Row(row =>
                        {
                            var links = new List<string>();
                            if (!string.IsNullOrWhiteSpace(data.Profile.LinkedIn)) links.Add($"LinkedIn: {data.Profile.LinkedIn}");
                            if (!string.IsNullOrWhiteSpace(data.Profile.GitHub)) links.Add($"GitHub: {data.Profile.GitHub}");
                            if (!string.IsNullOrWhiteSpace(data.Profile.Portfolio)) links.Add($"Portfolio: {data.Profile.Portfolio}");
                            if (links.Any())
                                row.AutoItem().Text(string.Join(" | ", links)).FontSize(9).FontColor("#bdc3c7");
                        });
                    });

                    // Summary
                    if (!string.IsNullOrWhiteSpace(data.Profile.Summary))
                    {
                        main.Item().PaddingTop(16).Text(data.Profile.Summary).FontSize(10).FontColor(DarkGray);
                    }

                    // Sections in order
                    foreach (var section in data.SectionOrder.Any() ? data.SectionOrder : new List<string> { "education", "experience", "skills", "projects", "certifications" })
                    {
                        switch (section.ToLower())
                        {
                            case "education": AddEducationSection(main, data.Education, AccentColor); break;
                            case "experience": AddExperienceSection(main, data.Experience, AccentColor); break;
                            case "skills": AddSkillsSection(main, data.Skills, AccentColor); break;
                            case "projects": AddProjectsSection(main, data.Projects, AccentColor); break;
                            case "certifications": AddCertificationsSection(main, data.Certifications, AccentColor); break;
                        }
                    }
                });
            });
        });
    }

    // ========================
    // CLASSIC TEMPLATE
    // ========================
    private IDocument BuildClassicResume(ResumeDataDto data)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Georgia"));

                page.Content().Column(main =>
                {
                    // Name
                    main.Item().Text(data.Profile.FullName).FontSize(22).Bold().FontColor(PrimaryColor);
                    main.Item().PaddingBottom(4).BorderBottom(2).BorderColor(PrimaryColor);

                    // Contact
                    main.Item().PaddingTop(4).Row(row =>
                    {
                        var parts = new List<string>();
                        if (!string.IsNullOrWhiteSpace(data.Profile.Location)) parts.Add(data.Profile.Location);
                        if (!string.IsNullOrWhiteSpace(data.Profile.Phone)) parts.Add(data.Profile.Phone);
                        if (!string.IsNullOrWhiteSpace(data.Profile.Email)) parts.Add(data.Profile.Email);
                        if (parts.Any())
                            row.AutoItem().Text(string.Join(" | ", parts)).FontSize(9).FontColor(DarkGray);
                    });
                    main.Item().Row(row =>
                    {
                        var links = new List<string>();
                        if (!string.IsNullOrWhiteSpace(data.Profile.LinkedIn)) links.Add($"LinkedIn: {data.Profile.LinkedIn}");
                        if (!string.IsNullOrWhiteSpace(data.Profile.GitHub)) links.Add($"GitHub: {data.Profile.GitHub}");
                        if (!string.IsNullOrWhiteSpace(data.Profile.Portfolio)) links.Add($"Portfolio: {data.Profile.Portfolio}");
                        if (links.Any())
                            row.AutoItem().Text(string.Join(" | ", links)).FontSize(9).FontColor(DarkGray);
                    });

                    // Summary
                    if (!string.IsNullOrWhiteSpace(data.Profile.Summary))
                    {
                        main.Item().PaddingTop(12).Text("Professional Summary").FontSize(14).Bold().FontColor(PrimaryColor);
                        main.Item().PaddingBottom(2).BorderBottom(1).BorderColor("#dddddd");
                        main.Item().PaddingTop(4).Text(data.Profile.Summary).FontSize(10);
                    }

                    foreach (var section in data.SectionOrder.Any() ? data.SectionOrder : new List<string> { "education", "experience", "skills", "projects", "certifications" })
                    {
                        switch (section.ToLower())
                        {
                            case "education": AddClassicEducation(main, data.Education); break;
                            case "experience": AddClassicExperience(main, data.Experience); break;
                            case "skills": AddClassicSkills(main, data.Skills); break;
                            case "projects": AddClassicProjects(main, data.Projects); break;
                            case "certifications": AddClassicCertifications(main, data.Certifications); break;
                        }
                    }
                });
            });
        });
    }

    // ========================
    // MINIMAL TEMPLATE
    // ========================
    private IDocument BuildMinimalResume(ResumeDataDto data)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(50);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Helvetica"));

                page.Content().Column(main =>
                {
                    main.Item().Text(data.Profile.FullName).FontSize(20).Bold().FontColor("#111111");
                    main.Item().PaddingTop(4).Row(row =>
                    {
                        var parts = new List<string>();
                        if (!string.IsNullOrWhiteSpace(data.Profile.Location)) parts.Add(data.Profile.Location);
                        if (!string.IsNullOrWhiteSpace(data.Profile.Phone)) parts.Add(data.Profile.Phone);
                        if (!string.IsNullOrWhiteSpace(data.Profile.Email)) parts.Add(data.Profile.Email);
                        if (parts.Any())
                            row.AutoItem().Text(string.Join("  ·  ", parts)).FontSize(9).FontColor(DarkGray);
                    });
                    main.Item().Row(row =>
                    {
                        var links = new List<string>();
                        if (!string.IsNullOrWhiteSpace(data.Profile.LinkedIn)) links.Add("LinkedIn");
                        if (!string.IsNullOrWhiteSpace(data.Profile.GitHub)) links.Add("GitHub");
                        if (!string.IsNullOrWhiteSpace(data.Profile.Portfolio)) links.Add("Portfolio");
                        if (links.Any())
                            row.AutoItem().Text(string.Join("  ·  ", links)).FontSize(9).FontColor(DarkGray);
                    });

                    if (!string.IsNullOrWhiteSpace(data.Profile.Summary))
                    {
                        main.Item().PaddingTop(16).Text(data.Profile.Summary).FontSize(10).Italic().FontColor(DarkGray);
                    }

                    foreach (var section in data.SectionOrder.Any() ? data.SectionOrder : new List<string> { "education", "experience", "skills", "projects", "certifications" })
                    {
                        switch (section.ToLower())
                        {
                            case "education": AddMinimalEducation(main, data.Education); break;
                            case "experience": AddMinimalExperience(main, data.Experience); break;
                            case "skills": AddMinimalSkills(main, data.Skills); break;
                            case "projects": AddMinimalProjects(main, data.Projects); break;
                            case "certifications": AddMinimalCertifications(main, data.Certifications); break;
                        }
                    }
                });
            });
        });
    }

    // ========================
    // COVER LETTER
    // ========================
    private IDocument BuildCoverLetter(CoverLetterDataDto data)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial").LineHeight(1.6f));

                page.Content().Column(main =>
                {
                    main.Item().AlignRight().Text(data.Date).FontSize(10).FontColor(DarkGray);
                    main.Item().PaddingTop(20).Text(data.Company).Bold().FontSize(11);
                    main.Item().Text(data.CompanyAddress).FontSize(10).FontColor(DarkGray);

                    main.Item().PaddingTop(24).Text("Dear Hiring Manager,").FontSize(11);

                    main.Item().PaddingTop(12).Text(data.Opening).FontSize(11);
                    main.Item().PaddingTop(12).Text(data.Body).FontSize(11);
                    main.Item().PaddingTop(12).Text(data.Closing).FontSize(11);

                    main.Item().PaddingTop(30).Text("Sincerely,").FontSize(11);
                    main.Item().PaddingTop(12).Text(data.Name).Bold().FontSize(11);

                    if (!string.IsNullOrWhiteSpace(data.Email))
                        main.Item().Text(data.Email).FontSize(10).FontColor(DarkGray);
                    if (!string.IsNullOrWhiteSpace(data.Phone))
                        main.Item().Text(data.Phone).FontSize(10).FontColor(DarkGray);
                });
            });
        });
    }

    // ========================
    // MODERN SECTION HELPERS
    // ========================
    private static void AddSectionHeader(ColumnDescriptor column, string title, string accentColor)
    {
        column.Item().PaddingTop(16).Background(accentColor).Padding(6).Text(title).FontSize(12).Bold().FontColor(Colors.White);
    }

    private static void AddEducationSection(ColumnDescriptor main, List<ResumeEducationDto> education, string accent)
    {
        if (!education.Any()) return;
        AddSectionHeader(main, "Education", accent);
        foreach (var edu in education)
        {
            main.Item().PaddingTop(8).Column(col =>
            {
                col.Item().Text(text =>
                {
                    text.Span(edu.Degree).Bold();
                    if (!string.IsNullOrWhiteSpace(edu.Field))
                        text.Span($" — {edu.Field}");
                });
                col.Item().Text(edu.School).Italic().FontColor(DarkGray);
                if (!string.IsNullOrWhiteSpace(edu.StartDate) || !string.IsNullOrWhiteSpace(edu.EndDate))
                {
                    var end = edu.IsCurrent ? "Present" : edu.EndDate;
                    col.Item().Text($"{edu.StartDate} — {end}").FontSize(9).FontColor(DarkGray);
                }
                if (!string.IsNullOrWhiteSpace(edu.Gpa) || !string.IsNullOrWhiteSpace(edu.Honors))
                {
                    var parts = new List<string>();
                    if (!string.IsNullOrWhiteSpace(edu.Gpa)) parts.Add($"GPA: {edu.Gpa}");
                    if (!string.IsNullOrWhiteSpace(edu.Honors)) parts.Add(edu.Honors);
                    col.Item().Text(string.Join(" | ", parts)).FontSize(9).FontColor(DarkGray);
                }
            });
        }
    }

    private static void AddExperienceSection(ColumnDescriptor main, List<ResumeExperienceDto> experience, string accent)
    {
        if (!experience.Any()) return;
        AddSectionHeader(main, "Experience", accent);
        foreach (var exp in experience)
        {
            main.Item().PaddingTop(8).Column(col =>
            {
                col.Item().Text(text =>
                {
                    text.Span(exp.Title).Bold();
                    text.Span($" — {exp.Company}");
                    if (!string.IsNullOrWhiteSpace(exp.Location))
                        text.Span($" ({exp.Location})").FontColor(DarkGray);
                });
                var end = exp.IsCurrent ? "Present" : exp.EndDate;
                col.Item().Text($"{exp.StartDate} — {end}").FontSize(9).FontColor(DarkGray);
                foreach (var bullet in exp.BulletList)
                {
                    col.Item().PaddingLeft(12).Text($"• {bullet}").FontSize(9);
                }
            });
        }
    }

    private static void AddSkillsSection(ColumnDescriptor main, List<ResumeSkillGroupDto> skills, string accent)
    {
        if (!skills.Any()) return;
        AddSectionHeader(main, "Technical Skills", accent);
        main.Item().PaddingTop(8).Column(col =>
        {
            foreach (var group in skills)
            {
                col.Item().Text(text =>
                {
                    text.DefaultTextStyle(x => x.FontSize(9));
                    text.Span($"{group.Category}: ").Bold();
                    text.Span(string.Join(", ", group.Skills));
                });
            }
        });
    }

    private static void AddProjectsSection(ColumnDescriptor main, List<ResumeProjectDto> projects, string accent)
    {
        if (!projects.Any()) return;
        AddSectionHeader(main, "Projects", accent);
        foreach (var proj in projects)
        {
            main.Item().PaddingTop(8).Column(col =>
            {
                col.Item().Text(text =>
                {
                    text.Span(proj.Name).Bold();
                    if (!string.IsNullOrWhiteSpace(proj.Link))
                        text.Span($" — {proj.Link}").FontSize(9).FontColor(DarkGray);
                });
                if (!string.IsNullOrWhiteSpace(proj.Technologies))
                    col.Item().Text($"Technologies: {proj.Technologies}").FontSize(9).FontColor(DarkGray);
                if (!string.IsNullOrWhiteSpace(proj.Description))
                    col.Item().Text(proj.Description).FontSize(9);
            });
        }
    }

    private static void AddCertificationsSection(ColumnDescriptor main, List<ResumeCertificationDto> certs, string accent)
    {
        if (!certs.Any()) return;
        AddSectionHeader(main, "Certifications", accent);
        main.Item().PaddingTop(8).Column(col =>
        {
            foreach (var cert in certs)
            {
                col.Item().Text($"• {cert.Name} — {cert.Organization} ({cert.Date})").FontSize(9);
            }
        });
    }

    // ========================
    // CLASSIC SECTION HELPERS
    // ========================
    private static void AddClassicSectionHeader(ColumnDescriptor main, string title)
    {
        main.Item().PaddingTop(14).Text(title).FontSize(13).Bold().FontColor(PrimaryColor);
        main.Item().PaddingBottom(2).BorderBottom(1).BorderColor("#dddddd");
    }

    private static void AddClassicEducation(ColumnDescriptor main, List<ResumeEducationDto> education)
    {
        if (!education.Any()) return;
        AddClassicSectionHeader(main, "Education");
        foreach (var edu in education)
        {
            main.Item().PaddingTop(6).Column(col =>
            {
                col.Item().Text(text =>
                {
                    text.Span(edu.Degree).Bold();
                    if (!string.IsNullOrWhiteSpace(edu.Field)) text.Span($" — {edu.Field}");
                });
                col.Item().Text(edu.School).Italic().FontColor(DarkGray);
                if (!string.IsNullOrWhiteSpace(edu.StartDate) || !string.IsNullOrWhiteSpace(edu.EndDate))
                {
                    var end = edu.IsCurrent ? "Present" : edu.EndDate;
                    col.Item().Text($"{edu.StartDate} — {end}").FontSize(9).FontColor(DarkGray);
                }
                if (!string.IsNullOrWhiteSpace(edu.Gpa) || !string.IsNullOrWhiteSpace(edu.Honors))
                {
                    var parts = new List<string>();
                    if (!string.IsNullOrWhiteSpace(edu.Gpa)) parts.Add($"GPA: {edu.Gpa}");
                    if (!string.IsNullOrWhiteSpace(edu.Honors)) parts.Add(edu.Honors);
                    col.Item().Text(string.Join(" | ", parts)).FontSize(9).FontColor(DarkGray);
                }
            });
        }
    }

    private static void AddClassicExperience(ColumnDescriptor main, List<ResumeExperienceDto> experience)
    {
        if (!experience.Any()) return;
        AddClassicSectionHeader(main, "Work Experience");
        foreach (var exp in experience)
        {
            main.Item().PaddingTop(6).Column(col =>
            {
                col.Item().Text(text =>
                {
                    text.Span(exp.Title).Bold();
                    text.Span($" — {exp.Company}");
                    if (!string.IsNullOrWhiteSpace(exp.Location))
                        text.Span($" ({exp.Location})").FontColor(DarkGray);
                });
                var end = exp.IsCurrent ? "Present" : exp.EndDate;
                col.Item().Text($"{exp.StartDate} — {end}").FontSize(9).FontColor(DarkGray);
                foreach (var bullet in exp.BulletList)
                {
                    col.Item().PaddingLeft(12).Text($"• {bullet}").FontSize(9);
                }
            });
        }
    }

    private static void AddClassicSkills(ColumnDescriptor main, List<ResumeSkillGroupDto> skills)
    {
        if (!skills.Any()) return;
        AddClassicSectionHeader(main, "Technical Skills");
        main.Item().PaddingTop(6).Column(col =>
        {
            foreach (var group in skills)
            {
                col.Item().Text(text =>
                {
                    text.DefaultTextStyle(x => x.FontSize(9));
                    text.Span($"{group.Category}: ").Bold();
                    text.Span(string.Join(", ", group.Skills));
                });
            }
        });
    }

    private static void AddClassicProjects(ColumnDescriptor main, List<ResumeProjectDto> projects)
    {
        if (!projects.Any()) return;
        AddClassicSectionHeader(main, "Projects");
        foreach (var proj in projects)
        {
            main.Item().PaddingTop(6).Column(col =>
            {
                col.Item().Text(text =>
                {
                    text.Span(proj.Name).Bold();
                    if (!string.IsNullOrWhiteSpace(proj.Link))
                        text.Span($" — {proj.Link}").FontSize(9).FontColor(DarkGray);
                });
                if (!string.IsNullOrWhiteSpace(proj.Technologies))
                    col.Item().Text($"Technologies: {proj.Technologies}").FontSize(9).FontColor(DarkGray);
                if (!string.IsNullOrWhiteSpace(proj.Description))
                    col.Item().Text(proj.Description).FontSize(9);
            });
        }
    }

    private static void AddClassicCertifications(ColumnDescriptor main, List<ResumeCertificationDto> certs)
    {
        if (!certs.Any()) return;
        AddClassicSectionHeader(main, "Certifications");
        main.Item().PaddingTop(6).Column(col =>
        {
            foreach (var cert in certs)
            {
                col.Item().Text($"• {cert.Name} — {cert.Organization} ({cert.Date})").FontSize(9);
            }
        });
    }

    // ========================
    // MINIMAL SECTION HELPERS
    // ========================
    private static void AddMinimalSectionHeader(ColumnDescriptor main, string title)
    {
        main.Item().PaddingTop(18).Text(title).FontSize(11).Bold().FontColor("#111111");
        main.Item().PaddingBottom(2).Width(100).BorderBottom(1).BorderColor("#cccccc");
    }

    private static void AddMinimalEducation(ColumnDescriptor main, List<ResumeEducationDto> education)
    {
        if (!education.Any()) return;
        AddMinimalSectionHeader(main, "Education");
        foreach (var edu in education)
        {
            main.Item().PaddingTop(6).Column(col =>
            {
                col.Item().Text(text =>
                {
                    text.Span(edu.Degree).Bold();
                    if (!string.IsNullOrWhiteSpace(edu.Field)) text.Span($" — {edu.Field}");
                });
                col.Item().Text(edu.School).Italic().FontColor(DarkGray);
                if (!string.IsNullOrWhiteSpace(edu.StartDate) || !string.IsNullOrWhiteSpace(edu.EndDate))
                {
                    var end = edu.IsCurrent ? "Present" : edu.EndDate;
                    col.Item().Text($"{edu.StartDate} — {end}").FontSize(9).FontColor(DarkGray);
                }
            });
        }
    }

    private static void AddMinimalExperience(ColumnDescriptor main, List<ResumeExperienceDto> experience)
    {
        if (!experience.Any()) return;
        AddMinimalSectionHeader(main, "Experience");
        foreach (var exp in experience)
        {
            main.Item().PaddingTop(6).Column(col =>
            {
                col.Item().Text(text =>
                {
                    text.Span(exp.Title).Bold();
                    text.Span($" — {exp.Company}");
                });
                var end = exp.IsCurrent ? "Present" : exp.EndDate;
                col.Item().Text($"{exp.StartDate} — {end}").FontSize(9).FontColor(DarkGray);
                foreach (var bullet in exp.BulletList)
                {
                    col.Item().Text($"• {bullet}").FontSize(9);
                }
            });
        }
    }

    private static void AddMinimalSkills(ColumnDescriptor main, List<ResumeSkillGroupDto> skills)
    {
        if (!skills.Any()) return;
        AddMinimalSectionHeader(main, "Skills");
        main.Item().PaddingTop(6).Column(col =>
        {
            foreach (var group in skills)
            {
                col.Item().Text(text =>
                {
                    text.DefaultTextStyle(x => x.FontSize(9));
                    text.Span($"{group.Category}: ").Bold();
                    text.Span(string.Join(", ", group.Skills));
                });
            }
        });
    }

    private static void AddMinimalProjects(ColumnDescriptor main, List<ResumeProjectDto> projects)
    {
        if (!projects.Any()) return;
        AddMinimalSectionHeader(main, "Projects");
        foreach (var proj in projects)
        {
            main.Item().PaddingTop(6).Column(col =>
            {
                col.Item().Text(proj.Name).Bold();
                if (!string.IsNullOrWhiteSpace(proj.Technologies))
                    col.Item().Text(proj.Technologies).FontSize(9).FontColor(DarkGray);
                if (!string.IsNullOrWhiteSpace(proj.Description))
                    col.Item().Text(proj.Description).FontSize(9);
            });
        }
    }

    private static void AddMinimalCertifications(ColumnDescriptor main, List<ResumeCertificationDto> certs)
    {
        if (!certs.Any()) return;
        AddMinimalSectionHeader(main, "Certifications");
        main.Item().PaddingTop(6).Column(col =>
        {
            foreach (var cert in certs)
            {
                col.Item().Text($"{cert.Name} — {cert.Organization}").FontSize(9);
            }
        });
    }
}
