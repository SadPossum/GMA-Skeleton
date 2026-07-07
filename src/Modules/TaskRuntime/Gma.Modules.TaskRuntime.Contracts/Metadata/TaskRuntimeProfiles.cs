namespace Gma.Modules.TaskRuntime.Contracts;

using Gma.Framework.ModuleComposition;
using Gma.Framework.Tasks;

public static class TaskRuntimeProfiles
{
    public const string DefaultName = "default";

    public static ModuleProfileDescriptor Default { get; } = new(
        TaskRuntimeModuleMetadata.Name,
        DefaultName,
        requires:
        [
            TasksCompositionFeatures.RunStoreRequired(
                Provider(DefaultName),
                "TaskRuntime admin surfaces require the persisted task run store from Gma.Modules.TaskRuntime.Persistence."),
            TasksCompositionFeatures.RuntimeReporterRequired(
                Provider(DefaultName),
                "TaskRuntime admin surfaces expose task progress from the persisted task runtime reporter."),
            TasksCompositionFeatures.ControlChannelRequired(
                Provider(DefaultName),
                "TaskRuntime admin surfaces send task control messages through the persisted task control channel.")
        ],
        displayName: "TaskRuntime default",
        description: "Administration surface for persisted task runs, progress, retries, cancellation, and control messages.");

    private static string Provider(string profileName) => $"{TaskRuntimeModuleMetadata.Name}/{profileName}";
}
