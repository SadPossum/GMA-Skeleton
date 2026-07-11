namespace Host.Worker;

using Gma.Modules.Auth.Contracts;
using Gma.Modules.Auth.Persistence;
using Catalog.Application;
using Catalog.Contracts;
using Catalog.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ordering.Application;
using Ordering.Contracts;
using Ordering.Persistence;
using ServiceDefaults;
using Gma.Framework.Caching.Cqrs;
using Gma.Framework.Caching.Redis;
using Gma.Framework.Infrastructure;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Messaging.Nats.Aspire;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Tasks.Cqrs;
using Gma.Framework.Tasks.Infrastructure;
using Gma.Framework.Tenancy.Caching;
using Gma.Framework.Tenancy.Messaging.Infrastructure;
using Gma.Framework.Tenancy.Tasks;
using Gma.Modules.TaskRuntime.Application;
using Gma.Modules.TaskRuntime.Contracts;
using Gma.Modules.TaskRuntime.Persistence;
using TaskSamples.Application;

public static class WorkerHostBuilderExtensions
{
    public static IHostApplicationBuilder AddWorkerHost(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        WorkerHostOptions workerOptions = WorkerHostOptions.FromConfiguration(builder.Configuration);
        builder.Services.AddSingleton(workerOptions);

        builder.AddRedisCaching();
        builder.AddCachingCqrs();
        builder.AddGmaInfrastructure();
        builder.AddTenantCaching();
        builder.AddMessagingInfrastructure();
        builder.AddTenantAwareMessaging();
        builder.AddConfiguredNatsJetStreamMessaging();

        if (workerOptions.NatsConsumersEnabled)
        {
            builder.AddConfiguredNatsJetStreamConsumers();
        }

        AddConfiguredModuleGroups(builder, workerOptions);

        if (workerOptions.TaskWorkerEnabled)
        {
            builder.AddTenantTaskExecutionContext();
            builder.AddTaskCqrs();
            builder.AddTaskWorkerRuntime();
        }

        builder.AddServiceDefaults();
        return builder;
    }

    public static IHost LogWorkerStartupSummary(this IHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        WorkerHostOptions workerOptions = host.Services.GetRequiredService<WorkerHostOptions>();
        ILogger logger = host.Services
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Host.Worker");
        string moduleList = workerOptions.GetComposedModuleNames() is { Count: > 0 } modules
            ? string.Join(", ", modules)
            : "none";

        logger.LogInformation(
            "Host.Worker starting. NATS publishing enabled: {NatsPublishingEnabled}; NATS consumers enabled: {NatsConsumersEnabled}; task workers enabled: {TaskWorkerEnabled}; composed modules: {WorkerModules}.",
            workerOptions.NatsPublishingEnabled,
            workerOptions.NatsConsumersEnabled,
            workerOptions.TaskWorkerEnabled,
            moduleList);

        return host;
    }

    private static void AddConfiguredModuleGroups(IHostApplicationBuilder builder, WorkerHostOptions workerOptions)
    {
        if (workerOptions.Modules.Auth)
        {
            builder.SelectModuleProfile(AuthProfile.ScopeAware().Descriptor, "Host.Worker/Auth");
            builder.AddAuthPersistence();
        }

        if (workerOptions.Modules.Catalog)
        {
            builder.SelectModuleProfile(CatalogProfiles.Default, "Host.Worker/Catalog");
            builder.Services.AddCatalogApplication();
            builder.AddCatalogPersistence();
        }

        if (workerOptions.Modules.Ordering)
        {
            builder.SelectModuleProfile(OrderingProfiles.Default, "Host.Worker/Ordering");
            builder.Services.AddOrderingApplication();
            if (workerOptions.TaskWorkerEnabled)
            {
                builder.Services.AddOrderingTaskHandlers();
            }
            builder.AddOrderingPersistence();
        }

        if (workerOptions.Modules.TaskRuntime)
        {
            builder.SelectModuleProfile(TaskRuntimeProfiles.Default, "Host.Worker/TaskRuntime");
            builder.Services.AddTaskRuntimeApplication();
            builder.AddTaskRuntimePersistence();
        }

        if (workerOptions.Modules.TaskSamples)
        {
            builder.AddTaskCqrs();
            builder.Services.AddTaskSamplesApplication();
            if (workerOptions.TaskWorkerEnabled)
            {
                builder.Services.AddTaskSamplesTaskHandlers();
            }
        }
    }
}
