namespace TaskSamples.Contracts;

using Gma.Framework.Modules;
using Gma.Framework.Tasks;

public static class TaskSamplesModuleMetadata
{
    public const string Name = "task-samples";
    public const string WorkerGroup = "samples";

    public static ModuleDescriptor Descriptor { get; } = ModuleDescriptor
        .Create(Name)
        .WithTask<GenerateReportTaskPayload>()
        .WithTask<GenerateReportTaskPayloadV2>()
        .WithTask<FlakyReportTaskPayload>()
        .WithTask<SlowReportTaskPayload>()
        .Build();
}
