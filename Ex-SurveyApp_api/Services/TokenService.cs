using Ex_SurveyApp_api.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Ex_SurveyApp_api.Services;

public record JwtSettings(string Issuer, string Audience, string SigningKey);

public class TokenService(JwtSettings settings)
{
    public (string Token, DateTimeOffset ExpiresAt) Create(User user)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience, claims,
            expires: expiresAt.UtcDateTime, signingCredentials: credentials);
        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
