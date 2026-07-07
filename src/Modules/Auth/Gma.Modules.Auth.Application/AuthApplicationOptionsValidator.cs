namespace Gma.Modules.Auth.Application;

using Microsoft.Extensions.Options;

internal sealed class AuthApplicationOptionsValidator : IValidateOptions<AuthApplicationOptions>
{
    public ValidateOptionsResult Validate(string? name, AuthApplicationOptions options)
    {
        if (options.RefreshTokenLifetimeDays <= 0)
        {
            return ValidateOptionsResult.Fail(
                $"{AuthApplicationOptions.SectionName}:RefreshTokenLifetimeDays must be positive.");
        }

        return ValidateOptionsResult.Success;
    }
}
