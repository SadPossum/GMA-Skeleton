namespace Architecture.Tests;

using Host.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Gma.Framework.Messaging;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Tasks;
using Xunit;

[Trait("Category", "Architecture")]
public sealed class WorkerHostCompositionTests
{
    private static readonly string[] AuthModuleNames = ["auth"];
    private static readonly string[] CatalogOrderingModuleNames = ["catalog", "ordering"];
    private static readonly string[] TaskRuntimeModuleNames = ["task-runtime"];

    [Fact]
    public async Task Worker_starts_with_background_loops_disabled_by_default()
    {
        HostApplicationBuilder builder = CreateBuilder();

        builder.AddWorkerHost();
        ModuleCompositionValidationResult result = builder.ValidateModuleComposition();

        Assert.True(result.IsValid, result.Report);
        AssertNoHostedService(builder, "OutboxPublisherService");
        AssertNoHostedService(builder, "NatsJetStreamConsumerService");
        AssertNoHostedService(builder, "TaskWorkerService");
        Assert.Empty(GetWorkerOptions(builder).GetComposedModuleNames());

        using IHost host = builder.Build();
        await host.StartAsync();
        await host.StopAsync();
    }

    [Fact]
    public void Worker_registers_outbox_publisher_only_when_nats_publishing_is_enabled()
    {
        HostApplicationBuilder builder = CreateBuilder();
        builder.Configuration["NatsJetStream:Enabled"] = "true";
        builder.Configuration["ConnectionStrings:nats"] = "nats://localhost:4222";
        builder.Configuration["Worker:Modules:Auth"] = "true";
        builder.Configuration["ConnectionStrings:SqlServer"] = "Server=localhost;Database=gma;Trusted_Connection=True;TrustServerCertificate=True";

        builder.AddWorkerHost();
        ModuleCompositionValidationResult result = builder.ValidateModuleComposition();

        Assert.True(result.IsValid, result.Report);
        AssertHostedService(builder, "OutboxPublisherService");
        AssertNoHostedService(builder, "NatsJetStreamConsumerService");
        Assert.Contains(builder.Services, descriptor =>
            descriptor.ServiceType == typeof(IEventBus) &&
            descriptor.ImplementationType?.Name == "NatsJetStreamEventBus");
        Assert.Equal(AuthModuleNames, GetWorkerOptions(builder).GetComposedModuleNames());
    }

    [Fact]
    public void Worker_can_register_consumers_without_outbox_publishing()
    {
        HostApplicationBuilder builder = CreateBuilder();
        builder.Configuration["NatsJetStream:Enabled"] = "false";
        builder.Configuration["NatsConsumers:Enabled"] = "true";
        builder.Configuration["ConnectionStrings:nats"] = "nats://localhost:4222";
        builder.Configuration["Worker:Modules:Catalog"] = "true";
        builder.Configuration["Worker:Modules:Ordering"] = "true";
        builder.Configuration["ConnectionStrings:SqlServer"] = "Server=localhost;Database=gma;Trusted_Connection=True;TrustServerCertificate=True";

        builder.AddWorkerHost();
        ModuleCompositionValidationResult result = builder.ValidateModuleComposition();

        Assert.True(result.IsValid, result.Report);
        AssertHostedService(builder, "NatsJetStreamConsumerService");
        AssertNoHostedService(builder, "OutboxPublisherService");
        Assert.DoesNotContain(builder.Services, descriptor =>
            descriptor.ServiceType == typeof(IEventBus) &&
            descriptor.ImplementationType?.Name == "NatsJetStreamEventBus");
        Assert.Equal(CatalogOrderingModuleNames, GetWorkerOptions(builder).GetComposedModuleNames());
    }

    [Fact]
    public void Worker_consumers_fail_fast_without_nats_connection()
    {
        HostApplicationBuilder builder = CreateBuilder();
        builder.Configuration["NatsConsumers:Enabled"] = "true";

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
            builder.AddWorkerHost());

        Assert.Contains(exception.Failures, failure => failure.Contains("ConnectionStrings:nats", StringComparison.Ordinal));
    }

    [Fact]
    public void Worker_task_runtime_requires_persisted_run_store_when_enabled()
    {
        HostApplicationBuilder builder = CreateBuilder();
        builder.Configuration["Tasks:Worker:Enabled"] = "true";

        builder.AddWorkerHost();

        ModuleCompositionValidationException exception = Assert.Throws<ModuleCompositionValidationException>(
            () => builder.ValidateModuleComposition());
        Assert.Contains("tasks.run-store", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Worker_task_runtime_starts_when_task_runtime_module_is_composed()
    {
        HostApplicationBuilder builder = CreateBuilder();
        builder.Configuration["Tasks:Worker:Enabled"] = "true";
        builder.Configuration["Worker:Modules:TaskRuntime"] = "true";
        builder.Configuration["ConnectionStrings:SqlServer"] = "Server=localhost;Database=gma;Trusted_Connection=True;TrustServerCertificate=True";

        builder.AddWorkerHost();
        ModuleCompositionValidationResult result = builder.ValidateModuleComposition();

        Assert.True(result.IsValid, result.Report);
        AssertHostedService(builder, "TaskWorkerService");
        Assert.Contains(builder.Services, descriptor => descriptor.ServiceType == typeof(ITaskRunStore));
        Assert.Equal(TaskRuntimeModuleNames, GetWorkerOptions(builder).GetComposedModuleNames());
    }

    [Fact]
    public void Worker_host_does_not_reference_front_door_modules_or_map_business_endpoints()
    {
        string repositoryRoot = FindRepositoryRoot();
        string workerRoot = Path.Combine(repositoryRoot, "src", "Host.Worker");
        string project = File.ReadAllText(Path.Combine(workerRoot, "Host.Worker.csproj"));
        string program = File.ReadAllText(Path.Combine(workerRoot, "Program.cs"));
        string composition = File.ReadAllText(Path.Combine(workerRoot, "WorkerHostBuilderExtensions.cs"));
        string combinedSource = program + composition;

        Assert.Contains("<Project Sdk=\"Microsoft.NET.Sdk\">", project, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.NET.Sdk.Web", project, StringComparison.Ordinal);
        Assert.DoesNotContain(".Api.csproj", project, StringComparison.Ordinal);
        Assert.DoesNotContain(".AdminApi.csproj", project, StringComparison.Ordinal);
        Assert.DoesNotContain("MapModules", combinedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("MapAdminApiModules", combinedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("MapGet", combinedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("MapPost", combinedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Aspire_worker_is_explicitly_opt_in_and_demonstrates_separated_publishing()
    {
        string repositoryRoot = FindRepositoryRoot();
        string appHostRoot = Path.Combine(repositoryRoot, "src", "AppHost");
        string appsettings = File.ReadAllText(Path.Combine(appHostRoot, "appsettings.json"));
        string program = File.ReadAllText(Path.Combine(appHostRoot, "Program.cs"));
        string project = File.ReadAllText(Path.Combine(appHostRoot, "AppHost.csproj"));

        Assert.Contains("\"Worker\"", appsettings, StringComparison.Ordinal);
        Assert.Contains("\"Enabled\": false", appsettings, StringComparison.Ordinal);
        Assert.Contains("AppHost:Worker:Enabled", program, StringComparison.Ordinal);
        Assert.Contains("Projects.Host_Worker", program, StringComparison.Ordinal);
        Assert.Contains("host-worker", program, StringComparison.Ordinal);
        Assert.Contains("workerEnabled ? \"false\" : \"true\"", program, StringComparison.Ordinal);
        Assert.Contains("Worker__Modules__Auth", program, StringComparison.Ordinal);
        Assert.Contains("..\\Host.Worker\\Host.Worker.csproj", project, StringComparison.Ordinal);
    }

    private static HostApplicationBuilder CreateBuilder() =>
        Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Unit"
        });

    private static WorkerHostOptions GetWorkerOptions(HostApplicationBuilder builder) =>
        Assert.IsType<WorkerHostOptions>(Assert.Single(
            builder.Services,
            descriptor => descriptor.ServiceType == typeof(WorkerHostOptions)).ImplementationInstance);

    private static void AssertHostedService(HostApplicationBuilder builder, string implementationName) =>
        Assert.Contains(builder.Services, descriptor =>
            descriptor.ServiceType == typeof(IHostedService) &&
            string.Equals(descriptor.ImplementationType?.Name, implementationName, StringComparison.Ordinal));

    private static void AssertNoHostedService(HostApplicationBuilder builder, string implementationName) =>
        Assert.DoesNotContain(builder.Services, descriptor =>
            descriptor.ServiceType == typeof(IHostedService) &&
            string.Equals(descriptor.ImplementationType?.Name, implementationName, StringComparison.Ordinal));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(Directory.GetCurrentDirectory());

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "GenericModularApi.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
