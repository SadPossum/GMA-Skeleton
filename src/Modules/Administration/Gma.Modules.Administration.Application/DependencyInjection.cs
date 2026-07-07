namespace Gma.Modules.Administration.Application;

using Gma.Modules.Administration.Application.Handlers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Gma.Framework.Administration;
using Gma.Framework.Application.Composition;

public static class DependencyInjection
{
    public static IServiceCollection AddAdministrationApplication(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        if (!services.Any(descriptor => descriptor.ServiceType == typeof(AdministrationOptionsRegistrationMarker)))
        {
            AdministrationOptionsValidation.GetValidatedOptions(configuration);
            services.AddSingleton<AdministrationOptionsRegistrationMarker>();
            services
                .AddOptions<AdministrationOptions>()
                .Bind(configuration.GetSection(AdministrationOptions.SectionName))
                .ValidateOnStart();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IValidateOptions<AdministrationOptions>, AdministrationOptionsValidator>());
        }

        services.Replace(ServiceDescriptor.Scoped<IAdminAuthorizationService, PersistedAdminAuthorizationService>());
        services.AddApplicationServicesFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }

    private sealed class AdministrationOptionsRegistrationMarker;
}
