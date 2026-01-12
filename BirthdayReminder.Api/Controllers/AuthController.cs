using BirthdayReminder.Infrastructure.Persistence;
using BirthdayReminder.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.RateLimiting;

namespace BirthdayReminder.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly AppDbContext _db;
    private readonly IConfiguration _cfg;

    public AuthController(UserManager<AppUser> userManager, AppDbContext db, IConfiguration cfg)
    {
        _userManager = userManager;
        _db = db;
        _cfg = cfg;
    }

    public record RegisterRequest(string Email, string Password);
    public record LoginRequest(string Email, string Password);
    public record AuthResponse(string AccessToken, string RefreshToken);

    [HttpPost("register")]
    [AllowAnonymous]

[EnableRateLimiting("auth-register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest req)
    {
        var user = new AppUser { UserName = req.Email, Email = req.Email };
        var result = await _userManager.CreateAsync(user, req.Password);
        if (!result.Succeeded) return BadRequest(result.Errors);

        return await IssueTokens(user);
    }

    [HttpPost("login")]
    [AllowAnonymous]

[EnableRateLimiting("auth-login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest req)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(x => x.Email == req.Email);
        if (user == null) return Unauthorized();

        var ok = await _userManager.CheckPasswordAsync(user, req.Password);
        if (!ok) return Unauthorized();

        return await IssueTokens(user);
    }

    public record RefreshRequest(string RefreshToken);

    [HttpPost("refresh")]
[EnableRateLimiting("auth-refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest req)
    {
        var tokenHash = Sha256(req.RefreshToken);

        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash);
        if (stored == null || stored.IsRevoked || stored.ExpiresAtUtc <= DateTime.UtcNow)
            return Unauthorized();

        stored.RevokedAtUtc = DateTime.UtcNow;

        var user = await _userManager.FindByIdAsync(stored.UserId.ToString());
        if (user == null) return Unauthorized();

        await _db.SaveChangesAsync();
        return await IssueTokens(user);
    }

[HttpPost("logout")]
    [Authorize]

[EnableRateLimiting("auth-logout")]
public async Task<IActionResult> Logout([FromBody] RefreshRequest req)
{
    if (req is null || string.IsNullOrWhiteSpace(req.RefreshToken))
        return BadRequest(new { message = "refreshToken is required" });

    var tokenHash = Sha256(req.RefreshToken);

    var stored = await _db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash);
    if (stored == null) return NoContent();

    var userIdStr = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrWhiteSpace(userIdStr)) return Unauthorized();

    if (stored.UserId != Guid.Parse(userIdStr))
        return Forbid();

    if (!stored.IsRevoked)
    {
        stored.RevokedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    return NoContent();
}


[HttpPost("logout-all")]
[Authorize]
[EnableRateLimiting("auth-logout")]
public async Task<IActionResult> LogoutAll()
{
    var userIdStr = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrWhiteSpace(userIdStr)) return Unauthorized();
    var userId = Guid.Parse(userIdStr);

    var tokens = await _db.RefreshTokens
        .Where(x => x.UserId == userId && x.RevokedAtUtc == null && x.ExpiresAtUtc > DateTime.UtcNow)
        .ToListAsync();

    if (tokens.Count == 0) return NoContent();

    var now = DateTime.UtcNow;
    foreach (var t in tokens)
        t.RevokedAtUtc = now;

    await _db.SaveChangesAsync();
    return NoContent();
}

    private async Task<AuthResponse> IssueTokens(AppUser user)
    {
        var jwt = _cfg.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
{
    // Standart JWT subject
    new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),

    // .NET ekosisteminde çok yaygın kullanılan claim
    new(ClaimTypes.NameIdentifier, user.Id.ToString()),

    new(JwtRegisteredClaimNames.Email, user.Email ?? "")
};


        var accessMinutes = int.Parse(jwt["AccessTokenMinutes"]!);
        var accessToken = new JwtSecurityToken(
            issuer: jwt["Issuer"],
            audience: jwt["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(accessMinutes),
            signingCredentials: creds
        );

        var accessTokenStr = new JwtSecurityTokenHandler().WriteToken(accessToken);

        var refreshDays = int.Parse(jwt["RefreshTokenDays"]!);
        var refreshTokenStr = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = Sha256(refreshTokenStr),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(refreshDays)
        });

        await _db.SaveChangesAsync();

        return new AuthResponse(accessTokenStr, refreshTokenStr);
    }

    private static string Sha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
    
}
