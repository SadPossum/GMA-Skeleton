namespace Gma.Modules.Auth.Infrastructure;

using Gma.Modules.Auth.Domain.Services;
using Gma.Modules.Auth.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Gma.Framework.Runtime;

public static class DependencyInjection
{
    public static IServiceCollection AddAuthInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        if (services.Any(descriptor => descriptor.ServiceType == typeof(AuthInfrastructureRegistrationMarker)))
        {
            return services;
        }

        AuthInfrastructureOptionsValidation.Validate(configuration);
        ApplicationIdentityOptions applicationIdentity = configuration
            .GetSection(ApplicationIdentityOptions.SectionName)
            .Get<ApplicationIdentityOptions>() ?? new ApplicationIdentityOptions();
        services.AddSingleton<AuthInfrastructureRegistrationMarker>();
        services
            .AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .PostConfigure(options => AuthInfrastructureOptionsValidation.ApplyJwtIdentityDefaults(options, applicationIdentity))
            .ValidateOnStart();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<JwtSettings>, JwtSettingsValidator>());
        services
            .AddOptions<RefreshTokenHashingOptions>()
            .Bind(configuration.GetSection(RefreshTokenHashingOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<RefreshTokenHashingOptions>, RefreshTokenHashingOptionsValidator>());
        services.TryAddScoped<IPasswordHashingService, PasswordHashingService>();
        services.TryAddScoped<IRefreshTokenHashingService, RefreshTokenHashingService>();
        services.TryAddScoped<ITokenService, JwtTokenService>();

        return services;
    }

    private sealed class AuthInfrastructureRegistrationMarker;
}
