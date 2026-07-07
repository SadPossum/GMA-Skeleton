namespace Gma.Modules.Auth.Infrastructure;

public sealed class JwtSettings
{
    public const string SectionName = "Auth:Jwt";
    public const int MinimumSigningKeyBytes = 32;

    public string? Issuer { get; set; }
    public string? Audience { get; set; }
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenLifetimeMinutes { get; set; } = 15;
}
