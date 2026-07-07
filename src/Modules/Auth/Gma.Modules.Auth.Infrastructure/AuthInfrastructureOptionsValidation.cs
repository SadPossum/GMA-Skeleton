namespace Gma.Modules.Auth.Infrastructure;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Gma.Framework.Runtime;

internal static class AuthInfrastructureOptionsValidation
{
    public static void Validate(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        JwtSettings jwtSettings = configuration
            .GetSection(JwtSettings.SectionName)
            .Get<JwtSettings>() ?? new JwtSettings();
        ApplicationIdentityOptions applicationIdentity = configuration
            .GetSection(ApplicationIdentityOptions.SectionName)
            .Get<ApplicationIdentityOptions>() ?? new ApplicationIdentityOptions();
        RefreshTokenHashingOptions refreshTokenHashingOptions = configuration
            .GetSection(RefreshTokenHashingOptions.SectionName)
            .Get<RefreshTokenHashingOptions>() ?? new RefreshTokenHashingOptions();

        ApplyJwtIdentityDefaults(jwtSettings, applicationIdentity);
        ValidateJwtSettings(jwtSettings);
        ValidateRefreshTokenHashingOptions(refreshTokenHashingOptions);
    }

    public static void ApplyJwtIdentityDefaults(JwtSettings options, ApplicationIdentityOptions applicationIdentity)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(applicationIdentity);

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            options.Issuer = applicationIdentity.EffectiveDisplayName;
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            options.Audience = applicationIdentity.EffectiveDisplayName;
        }
    }

    private static void ValidateJwtSettings(JwtSettings options)
    {
        ValidateOptionsResult result = new JwtSettingsValidator().Validate(name: null, options);

        if (result.Failed)
        {
            throw new OptionsValidationException(JwtSettings.SectionName, typeof(JwtSettings), result.Failures);
        }
    }

    private static void ValidateRefreshTokenHashingOptions(RefreshTokenHashingOptions options)
    {
        ValidateOptionsResult result = new RefreshTokenHashingOptionsValidator().Validate(name: null, options);

        if (result.Failed)
        {
            throw new OptionsValidationException(
                RefreshTokenHashingOptions.SectionName,
                typeof(RefreshTokenHashingOptions),
                result.Failures);
        }
    }
}
