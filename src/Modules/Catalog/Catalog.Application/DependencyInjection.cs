namespace Catalog.Application;

using Catalog.Contracts;
using Gma.Framework.AccessControl;
using Microsoft.Extensions.DependencyInjection;
using Gma.Framework.Application.Composition;

public static class DependencyInjection
{
    public static IServiceCollection AddCatalogApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddGmaAccessControlPermissionPolicies(CatalogModuleMetadata.Descriptor);
        services.AddApplicationServicesFromAssembly(typeof(DependencyInjection).Assembly);
        return services;
    }
}
