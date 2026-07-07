namespace Gma.Modules.Auth.Infrastructure;

using System.Text;
using Microsoft.Extensions.Options;

internal sealed class JwtSettingsValidator : IValidateOptions<JwtSettings>
{
    public ValidateOptionsResult Validate(string? name, JwtSettings options)
    {
        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            return ValidateOptionsResult.Fail($"{JwtSettings.SectionName}:Issuer is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            return ValidateOptionsResult.Fail($"{JwtSettings.SectionName}:Audience is required.");
        }

        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            return ValidateOptionsResult.Fail($"{JwtSettings.SectionName}:SigningKey is required.");
        }

        if (Encoding.UTF8.GetByteCount(options.SigningKey) < JwtSettings.MinimumSigningKeyBytes)
        {
            return ValidateOptionsResult.Fail(
                $"{JwtSettings.SectionName}:SigningKey must be at least {JwtSettings.MinimumSigningKeyBytes} bytes.");
        }

        if (options.AccessTokenLifetimeMinutes <= 0)
        {
            return ValidateOptionsResult.Fail($"{JwtSettings.SectionName}:AccessTokenLifetimeMinutes must be positive.");
        }

        return ValidateOptionsResult.Success;
    }
}
