namespace Host.Worker;

using Microsoft.Extensions.Configuration;

public sealed class WorkerHostOptions
{
    public const string SectionName = "Worker";

    private WorkerHostOptions(
        WorkerModuleOptions modules,
        bool natsPublishingEnabled,
        bool natsConsumersEnabled,
        bool taskWorkerEnabled)
    {
        this.Modules = modules;
        this.NatsPublishingEnabled = natsPublishingEnabled;
        this.NatsConsumersEnabled = natsConsumersEnabled;
        this.TaskWorkerEnabled = taskWorkerEnabled;
    }

    public WorkerModuleOptions Modules { get; }
    public bool NatsPublishingEnabled { get; }
    public bool NatsConsumersEnabled { get; }
    public bool TaskWorkerEnabled { get; }

    public static WorkerHostOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        IConfigurationSection modules = configuration.GetSection($"{SectionName}:Modules");
        WorkerModuleOptions moduleOptions = new(
            GetBoolean(modules, nameof(WorkerModuleOptions.Auth), defaultValue: false),
            GetBoolean(modules, nameof(WorkerModuleOptions.Notifications), defaultValue: false),
            GetBoolean(modules, nameof(WorkerModuleOptions.Catalog), defaultValue: false),
            GetBoolean(modules, nameof(WorkerModuleOptions.Ordering), defaultValue: false),
            GetBoolean(modules, nameof(WorkerModuleOptions.TaskRuntime), defaultValue: false),
            GetBoolean(modules, nameof(WorkerModuleOptions.TaskSamples), defaultValue: false));

        return new WorkerHostOptions(
            moduleOptions,
            GetBoolean(configuration, "NatsJetStream:Enabled", defaultValue: false),
            GetBoolean(configuration, "NatsConsumers:Enabled", defaultValue: false),
            GetBoolean(configuration, "Tasks:Worker:Enabled", defaultValue: false));
    }

    public IReadOnlyList<string> GetComposedModuleNames()
    {
        List<string> modules = [];

        if (this.Modules.Auth)
        {
            modules.Add("auth");
        }

        if (this.Modules.Catalog)
        {
            modules.Add("catalog");
        }

        if (this.Modules.Notifications)
        {
            modules.Add("notifications");
        }

        if (this.Modules.Ordering)
        {
            modules.Add("ordering");
        }

        if (this.Modules.TaskRuntime)
        {
            modules.Add("task-runtime");
        }

        if (this.Modules.TaskSamples)
        {
            modules.Add("task-samples");
        }

        return modules;
    }

    private static bool GetBoolean(IConfiguration configuration, string key, bool defaultValue)
    {
        string? value = configuration[key];
        return bool.TryParse(value, out bool parsed)
            ? parsed
            : defaultValue;
    }
}

public sealed record WorkerModuleOptions(
    bool Auth,
    bool Notifications,
    bool Catalog,
    bool Ordering,
    bool TaskRuntime,
    bool TaskSamples);
