using LifeOS.Application.DTOs.Profile;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly AppDbContext _context;

    public ProfileController(AppDbContext context)
    {
        _context = context;
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(sub!);
    }

    [HttpGet]
    public async Task<ActionResult<ProfileDto>> Get(CancellationToken ct)
    {
        var userId = GetUserId();
        var profile = await _context.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        if (profile == null)
            return NotFound();

        return Ok(MapToDto(profile));
    }

    [HttpPut]
    public async Task<ActionResult<ProfileDto>> Update(ProfileDto dto, CancellationToken ct)
    {
        var userId = GetUserId();
        var profile = await _context.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        if (profile == null)
        {
            profile = new UserProfile
            {
                UserId = userId,
                FullName = dto.FullName,
                Phone = dto.Phone,
                Location = dto.Location,
                LinkedInUrl = dto.LinkedInUrl,
                GitHubUrl = dto.GitHubUrl,
                PortfolioUrl = dto.PortfolioUrl,
                Summary = dto.Summary,
                TargetRoles = dto.TargetRoles,
                AvatarUrl = dto.AvatarUrl
            };
            _context.UserProfiles.Add(profile);
        }
        else
        {
            profile.FullName = dto.FullName;
            profile.Phone = dto.Phone;
            profile.Location = dto.Location;
            profile.LinkedInUrl = dto.LinkedInUrl;
            profile.GitHubUrl = dto.GitHubUrl;
            profile.PortfolioUrl = dto.PortfolioUrl;
            profile.Summary = dto.Summary;
            profile.TargetRoles = dto.TargetRoles;
            profile.AvatarUrl = dto.AvatarUrl;
        }

        await _context.SaveChangesAsync(ct);
        return Ok(MapToDto(profile));
    }

    private static ProfileDto MapToDto(UserProfile p) => new()
    {
        UserId = p.UserId,
        FullName = p.FullName,
        Phone = p.Phone,
        Location = p.Location,
        LinkedInUrl = p.LinkedInUrl,
        GitHubUrl = p.GitHubUrl,
        PortfolioUrl = p.PortfolioUrl,
        Summary = p.Summary,
        TargetRoles = p.TargetRoles,
        AvatarUrl = p.AvatarUrl
    };
}
