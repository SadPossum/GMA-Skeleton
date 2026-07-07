namespace Gma.Modules.Auth.Infrastructure;

using System.Text;
using Microsoft.IdentityModel.Tokens;

public static class JwtTokenValidationParametersFactory
{
    public static TokenValidationParameters Create(JwtSettings settings, bool validateLifetime) =>
        new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = validateLifetime,
            ValidIssuer = settings.Issuer,
            ValidAudience = settings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.Zero
        };
}
