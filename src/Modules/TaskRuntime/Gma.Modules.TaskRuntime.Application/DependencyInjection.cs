namespace Gma.Modules.TaskRuntime.Application;

using Microsoft.Extensions.DependencyInjection;
using Gma.Framework.Application.Composition;

public static class DependencyInjection
{
    public static IServiceCollection AddTaskRuntimeApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddApplicationServicesFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}
