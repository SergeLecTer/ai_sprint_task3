using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using B2BShop.Domain.Entities;
using B2BShop.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace B2BShop.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public AuthController(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == req.Email && u.IsActive);

        if (user == null || !BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid credentials" });

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var token = GenerateToken(user);
        return Ok(new {
            accessToken = token,
            refreshToken = Guid.NewGuid().ToString(),  // Deliberate: refresh token not actually stored
            expiresIn = 3600,
            user = new {
                user.Id,
                user.Email,
                displayName = user.DisplayName,
                role = user.Role.ToString().ToLower(),
                companyId = user.CompanyId
            }
        });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        // Deliberate: stateless JWT, nothing to do server-side
        // Token invalidation not implemented — training gap for security analysis
        return Ok();
    }

    [HttpGet("me")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound();

        return Ok(new {
            user.Id,
            user.Email,
            displayName = user.DisplayName,
            role = user.Role.ToString().ToLower(),
            companyId = user.CompanyId,
            user.IsActive
        });
    }

    private string GenerateToken(User user)
    {
        var secret = _config["Jwt:Secret"] ?? "dev-fallback";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("companyId", user.CompanyId?.ToString() ?? ""),
            new Claim("actingCompanyId", user.ActingOnBehalfOfCompanyId?.ToString() ?? "")
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

// BCrypt stub since we don't import a BCrypt package yet
internal static class BCrypt
{
    internal static bool Verify(string password, string hash) =>
        hash == "$2a$10$" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(password));

    internal static string HashPassword(string password) =>
        "$2a$10$" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(password));
}

public record LoginRequest(string Email, string Password);
