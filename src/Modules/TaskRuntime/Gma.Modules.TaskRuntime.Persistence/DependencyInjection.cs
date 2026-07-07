namespace Gma.Modules.TaskRuntime.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Gma.Framework.Tasks;
using Gma.Framework.Cqrs.UnitOfWork;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Persistence.EntityFrameworkCore;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddTaskRuntimePersistence(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddPersistenceOptions(builder.Configuration);
        builder.Services.TryAddModuleDbContext<TaskRuntimeDbContext>(options =>
            options.UseConfiguredProvider(
                builder.Configuration,
                TaskRuntimeMigrations.SqlServerAssembly,
                TaskRuntimeMigrations.PostgreSqlAssembly,
                TaskRuntimeMigrations.Schema,
                TaskRuntimeMigrations.HistoryTable));

        builder.Services.TryAddScoped<ITaskRunStore, TaskRuntimeRunStore>();
        builder.Services.TryAddScoped<ITaskRuntimeReporter>(provider => provider.GetRequiredService<ITaskRunStore>());
        builder.Services.TryAddScoped<ITaskControlChannel>(provider => provider.GetRequiredService<ITaskRunStore>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IUnitOfWork, TaskRuntimeUnitOfWork>());
        builder.ProvideFeature(TasksCompositionFeatures.RunStoreProvided("Gma.Modules.TaskRuntime.Persistence"));
        builder.ProvideFeature(TasksCompositionFeatures.RuntimeReporterProvided("Gma.Modules.TaskRuntime.Persistence"));
        builder.ProvideFeature(TasksCompositionFeatures.ControlChannelProvided("Gma.Modules.TaskRuntime.Persistence"));

        return builder;
    }
}
