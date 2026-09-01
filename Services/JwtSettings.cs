namespace Kanban.Services;

// Both the API's JWT bearer validation (Program.cs) and JwtTokenService's minting need the
// same Jwt:Issuer/Audience/Key config, resolved the same way — one signing key validates
// what the other issues.
public record JwtSettings(string Issuer, string Audience, string Key)
{
    public static JwtSettings FromConfiguration(IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection("Jwt");
        var issuer = jwtSection["Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer not configured");
        var audience = jwtSection["Audience"] ?? throw new InvalidOperationException("Jwt:Audience not configured");
        var key = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key not configured");
        return new JwtSettings(issuer, audience, key);
    }
}
