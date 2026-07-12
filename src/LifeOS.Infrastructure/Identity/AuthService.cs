using LifeOS.Application.DTOs.Auth;
using LifeOS.Application.Interfaces;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Infrastructure.Identity;

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly IJwtService _jwt;
    private readonly IPasswordHasher _hasher;

    public AuthService(AppDbContext context, IJwtService jwt, IPasswordHasher hasher)
    {
        _context = context;
        _jwt = jwt;
        _hasher = hasher;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        if (await _context.Users.AnyAsync(u => u.Username == request.Username || u.Email == request.Email, ct))
            throw new InvalidOperationException("Username or email already taken.");

        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = _hasher.Hash(request.Password),
            Role = "user"
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        return await GenerateAuthResponseAsync(user, ct);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _context.Users
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Username == request.UsernameOrEmail || u.Email == request.UsernameOrEmail, ct);

        if (user == null || !_hasher.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid credentials.");

        return await GenerateAuthResponseAsync(user, ct);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        var tokenHash = BCrypt.Net.BCrypt.HashPassword(request.RefreshToken);
        // Note: we cannot search by hash directly because BCrypt salts are random.
        // For simplicity in Phase 1, we iterate recent tokens. In production, use SHA256 of token as lookup key.
        var tokens = await _context.RefreshTokens
            .Include(rt => rt.User)
            .ThenInclude(u => u.Profile)
            .Where(rt => rt.RevokedAt == null && rt.ExpiresAt > DateTimeOffset.UtcNow)
            .OrderByDescending(rt => rt.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        var match = tokens.FirstOrDefault(rt => BCrypt.Net.BCrypt.Verify(request.RefreshToken, rt.TokenHash));
        if (match == null)
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");

        return await GenerateAuthResponseAsync(match.User, ct);
    }

    public async Task LogoutAsync(Guid userId, string refreshToken, CancellationToken ct = default)
    {
        var tokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .OrderByDescending(rt => rt.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        var match = tokens.FirstOrDefault(rt => BCrypt.Net.BCrypt.Verify(refreshToken, rt.TokenHash));
        if (match != null)
        {
            match.RevokedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task<User?> GetUserByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Users
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    private async Task<AuthResponse> GenerateAuthResponseAsync(User user, CancellationToken ct)
    {
        var accessToken = _jwt.GenerateAccessToken(user);
        var refreshTokenRaw = _jwt.GenerateRefreshToken();
        var refreshTokenHash = BCrypt.Net.BCrypt.HashPassword(refreshTokenRaw);

        var refresh = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
        };

        _context.RefreshTokens.Add(refresh);
        await _context.SaveChangesAsync(ct);

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenRaw,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15),
            User = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role,
                FullName = user.Profile?.FullName
            }
        };
    }
}
