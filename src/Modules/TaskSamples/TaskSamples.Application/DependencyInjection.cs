namespace TaskSamples.Application;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Gma.Framework.Application.Composition;
using Gma.Framework.Tasks;
using TaskSamples.Application.Tasks;
using TaskSamples.Contracts;

public static class DependencyInjection
{
    public static IServiceCollection AddTaskSamplesApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddApplicationServicesFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddTaskHandler<GenerateReportTaskPayload, GenerateReportTaskHandler>(TaskSamplesModuleMetadata.Name);
        services.AddTaskHandler<GenerateReportTaskPayloadV2, GenerateReportTaskV2Handler>(TaskSamplesModuleMetadata.Name);
        services.AddTaskHandler<FlakyReportTaskPayload, FlakyReportTaskHandler>(TaskSamplesModuleMetadata.Name);
        services.AddTaskHandler<SlowReportTaskPayload, SlowReportTaskHandler>(TaskSamplesModuleMetadata.Name);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITaskScheduleProvider, TaskSamplesScheduleProvider>());
        services.TryAddSingleton<ITaskSampleReportSink, NullTaskSampleReportSink>();

        return services;
    }
}
