namespace Gma.Modules.Auth.Application;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Gma.Framework.Application.Composition;

public static class DependencyInjection
{
    public static IServiceCollection AddAuthApplication(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        if (!services.Any(descriptor => descriptor.ServiceType == typeof(AuthApplicationOptionsRegistrationMarker)))
        {
            AuthApplicationOptionsValidation.GetValidatedOptions(configuration);
            services.AddSingleton<AuthApplicationOptionsRegistrationMarker>();
            services
                .AddOptions<AuthApplicationOptions>()
                .Bind(configuration.GetSection(AuthApplicationOptions.SectionName))
                .ValidateOnStart();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IValidateOptions<AuthApplicationOptions>, AuthApplicationOptionsValidator>());
        }

        services.AddApplicationServicesFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }

    private sealed class AuthApplicationOptionsRegistrationMarker;
}
