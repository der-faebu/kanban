using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Kanban.Services;

public interface IJwtTokenService
{
    string CreateToken(string userId);
}

public class JwtTokenService(IConfiguration configuration) : IJwtTokenService
{
    public string CreateToken(string userId)
    {
        var jwtSettings = JwtSettings.FromConfiguration(configuration);

        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId) };
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtSettings.Issuer,
            audience: jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
