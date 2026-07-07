namespace Catalog.Application;

using Microsoft.Extensions.DependencyInjection;
using Gma.Framework.Application.Composition;

public static class DependencyInjection
{
    public static IServiceCollection AddCatalogApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddApplicationServicesFromAssembly(typeof(DependencyInjection).Assembly);
        return services;
    }
}
