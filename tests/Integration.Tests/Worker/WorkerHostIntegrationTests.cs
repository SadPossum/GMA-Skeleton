namespace Integration.Tests;

using System.Text.Json;
using Gma.Modules.Auth.Persistence;
using Catalog.Persistence;
using DotNet.Testcontainers.Containers;
using Host.Worker;
using Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ordering.Contracts;
using Ordering.Persistence;
using Gma.Framework.Messaging;
using Gma.Framework.Messaging.Nats;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Tasks;
using Gma.Framework.Tasks.Infrastructure;
using Gma.Modules.TaskRuntime.Persistence;
using TaskSamples.Application.Tasks;
using TaskSamples.Contracts;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class WorkerHostIntegrationTests
{
    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Worker_host_starts_with_supported_features_and_processes_sample_task()
    {
        await using IContainer nats = AuthTestContainers.CreateNatsContainer();
        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("gma_worker_compatibility_tests")
            .Build();
        await nats.StartAsync().ConfigureAwait(false);
        await postgreSql.StartAsync().ConfigureAwait(false);

        RecordingTaskSampleReportSink sink = new();
        using IHost worker = BuildWorker(
            postgreSql.GetConnectionString(),
            AuthTestContainers.GetNatsConnectionString(nats),
            $"GMA_WORKER_COMPAT_{Guid.NewGuid():N}".ToUpperInvariant(),
            sink);
        Guid runId = Guid.Parse("abababab-abab-abab-abab-abababababab");

        await MigrateWorkerStoresAsync(worker).ConfigureAwait(false);
        await EnqueueSampleTaskAsync(worker, runId).ConfigureAwait(false);

        AssertWorkerCompatibilityRegistrations(worker);

        await worker.StartAsync().ConfigureAwait(false);
        try
        {
            IReadOnlyList<TaskSampleReport> reports =
                await sink.WaitForReportsAsync(1, TimeSpan.FromSeconds(20)).ConfigureAwait(false);
            TaskRunSnapshot snapshot = await WaitForStatusAsync(
                    worker,
                    runId,
                    TaskRunStatus.Succeeded,
                    TimeSpan.FromSeconds(20))
                .ConfigureAwait(false);

            Assert.Single(reports);
            Assert.Equal(runId, reports[0].RunId);
            Assert.Equal("tenant-worker", reports[0].ScopeId);
            Assert.Equal(TaskRunStatus.Succeeded, snapshot.Status);
            Assert.Equal(1, snapshot.Attempts);
            Assert.Null(snapshot.LockedBy);
            Assert.Null(snapshot.LastError);
        }
        finally
        {
            await worker.StopAsync().ConfigureAwait(false);
        }
    }

    private static IHost BuildWorker(
        string postgreSqlConnectionString,
        string natsConnectionString,
        string streamName,
        RecordingTaskSampleReportSink sink)
    {
        HostApplicationBuilder builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Integration",
        });
        builder.Configuration["Persistence:Provider"] = "PostgreSql";
        builder.Configuration["ConnectionStrings:PostgreSql"] = postgreSqlConnectionString;
        builder.Configuration["ConnectionStrings:nats"] = natsConnectionString;
        builder.Configuration["Tenancy:Enabled"] = "true";
        builder.Configuration["NatsJetStream:Enabled"] = "true";
        builder.Configuration["NatsJetStream:StreamName"] = streamName;
        builder.Configuration["NatsConsumers:Enabled"] = "true";
        builder.Configuration["NatsConsumers:FetchBatchSize"] = "1";
        builder.Configuration["NatsConsumers:PollInterval"] = "00:00:00.100";
        builder.Configuration["NatsConsumers:AckWait"] = "00:00:05";
        builder.Configuration["NatsConsumers:AckProgressInterval"] = "00:00:01";
        builder.Configuration["NatsConsumers:HandlerTimeout"] = "00:00:05";
        builder.Configuration["NatsConsumers:NakDelay"] = "00:00:00.100";
        builder.Configuration["Outbox:BatchSize"] = "5";
        builder.Configuration["Outbox:PollIntervalMilliseconds"] = "100";
        builder.Configuration["Outbox:LockDurationMilliseconds"] = "1000";
        builder.Configuration["Tasks:Worker:Enabled"] = "true";
        builder.Configuration["Tasks:Worker:WorkerGroups:0"] = TaskSamplesModuleMetadata.WorkerGroup;
        builder.Configuration["Tasks:Worker:BatchSize"] = "5";
        builder.Configuration["Tasks:Worker:MaxConcurrency"] = "1";
        builder.Configuration["Tasks:Worker:PollInterval"] = "00:00:00.100";
        builder.Configuration["Tasks:Worker:LeaseDuration"] = "00:00:05";
        builder.Configuration["Tasks:Worker:HandlerTimeout"] = "00:00:05";
        builder.Configuration["Tasks:Worker:RetryBaseDelay"] = "00:00:00.100";
        builder.Configuration["Tasks:Worker:RetryMaxDelay"] = "00:00:01";
        builder.Configuration["Tasks:Worker:WorkerId"] = "worker-compatibility-test";
        builder.Configuration["Tasks:Worker:NodeId"] = "worker-compatibility-node";
        builder.Configuration["Tasks:Worker:TimeoutScannerEnabled"] = "false";
        builder.Configuration["Tasks:Worker:MetricsSamplerEnabled"] = "false";
        builder.Configuration["Worker:Modules:Auth"] = "true";
        builder.Configuration["Worker:Modules:Catalog"] = "true";
        builder.Configuration["Worker:Modules:Ordering"] = "true";
        builder.Configuration["Worker:Modules:TaskRuntime"] = "true";
        builder.Configuration["Worker:Modules:TaskSamples"] = "true";
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(sink);
        builder.Services.AddSingleton<ITaskSampleReportSink>(sink);

        builder.AddWorkerHost();
        ModuleCompositionValidationResult result = builder.ValidateModuleComposition();
        Assert.True(result.IsValid, result.Report);

        return builder.Build();
    }

    private static async Task MigrateWorkerStoresAsync(IHost worker)
    {
        using IServiceScope scope = worker.Services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<AuthDbContext>()
            .Database
            .MigrateAsync()
            .ConfigureAwait(false);
        await scope.ServiceProvider.GetRequiredService<CatalogDbContext>()
            .Database
            .MigrateAsync()
            .ConfigureAwait(false);
        await scope.ServiceProvider.GetRequiredService<OrderingDbContext>()
            .Database
            .MigrateAsync()
            .ConfigureAwait(false);
        await scope.ServiceProvider.GetRequiredService<TaskRuntimeDbContext>()
            .Database
            .MigrateAsync()
            .ConfigureAwait(false);
    }

    private static async Task EnqueueSampleTaskAsync(IHost worker, Guid runId)
    {
        using IServiceScope scope = worker.Services.CreateScope();
        ITaskRunStore store = scope.ServiceProvider.GetRequiredService<ITaskRunStore>();
        DateTimeOffset createdAtUtc = DateTimeOffset.UtcNow;
        string payloadJson = JsonSerializer.Serialize(new GenerateReportTaskPayload("worker-compatibility", 12));

        await store.EnqueueAsync(
                new TaskRunRequest(
                    runId,
                    TaskSamplesModuleMetadata.Name,
                    GenerateReportTaskPayload.TaskName,
                    payloadJson,
                    createdAtUtc,
                    createdAtUtc,
                    TaskSamplesModuleMetadata.WorkerGroup,
                    scopeId: "tenant-worker",
                    requestedBy: "integration-test",
                    maxAttempts: 3,
                    payloadVersion: GenerateReportTaskPayload.PayloadVersion,
                    deduplicationKey: null),
                CancellationToken.None)
            .ConfigureAwait(false);
    }

    private static void AssertWorkerCompatibilityRegistrations(IHost worker)
    {
        string[] hostedServiceNames = worker.Services
            .GetServices<IHostedService>()
            .Select(service => service.GetType().Name)
            .ToArray();
        string[] outboxStores = worker.Services
            .GetServices<IOutboxStore>()
            .Select(store => store.ModuleName)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] inboxStores = worker.Services
            .GetServices<IInboxStore>()
            .Select(store => store.ModuleName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Contains("OutboxPublisherService", hostedServiceNames);
        Assert.Contains("NatsJetStreamConsumerService", hostedServiceNames);
        Assert.Contains("TaskWorkerService", hostedServiceNames);
        Assert.IsType<NatsJetStreamEventBus>(worker.Services.GetRequiredService<IEventBus>());
        Assert.NotEmpty(worker.Services.GetRequiredService<IIntegrationEventSubscriptionRegistry>().Subscriptions);
        Assert.Contains("auth", outboxStores);
        Assert.Contains("catalog", outboxStores);
        Assert.Contains("ordering", outboxStores);
        Assert.Contains("catalog", inboxStores);
        Assert.Contains("ordering", inboxStores);
        ITaskHandlerRegistry taskHandlers = worker.Services.GetRequiredService<ITaskHandlerRegistry>();
        Assert.NotNull(taskHandlers.Find(
            TaskSamplesModuleMetadata.Name,
            GenerateReportTaskPayload.TaskName,
            GenerateReportTaskPayload.PayloadVersion));
        Assert.NotNull(taskHandlers.Find(
            OrderingModuleMetadata.Name,
            RebuildCatalogItemProjectionPayload.TaskName,
            RebuildCatalogItemProjectionPayload.PayloadVersion));
    }

    private static async Task<TaskRunSnapshot> WaitForStatusAsync(
        IHost worker,
        Guid runId,
        TaskRunStatus expectedStatus,
        TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            TaskRunSnapshot snapshot = await GetSnapshotAsync(worker, runId).ConfigureAwait(false);
            if (snapshot.Status == expectedStatus)
            {
                return snapshot;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
        }

        return await GetSnapshotAsync(worker, runId).ConfigureAwait(false);
    }

    private static async Task<TaskRunSnapshot> GetSnapshotAsync(IHost worker, Guid runId)
    {
        using IServiceScope scope = worker.Services.CreateScope();
        TaskRuntimeDbContext dbContext = scope.ServiceProvider.GetRequiredService<TaskRuntimeDbContext>();
        TaskRunSnapshot? snapshot = await dbContext.TaskRuns
            .Where(taskRun => taskRun.Id == runId)
            .Select(taskRun => new TaskRunSnapshot(
                taskRun.Id,
                taskRun.Status,
                taskRun.LockedBy,
                taskRun.NodeId,
                taskRun.Attempts,
                taskRun.NextAttemptAtUtc,
                taskRun.CompletedAtUtc,
                taskRun.LastError,
                taskRun.ProgressPercent,
                taskRun.ProgressMessage,
                taskRun.RequestedBy,
                taskRun.CancellationRequestedBy,
                taskRun.CancellationRequestedAtUtc,
                taskRun.PayloadVersion,
                taskRun.DeduplicationKey))
            .SingleOrDefaultAsync()
            .ConfigureAwait(false);

        Assert.NotNull(snapshot);
        return snapshot;
    }
}
