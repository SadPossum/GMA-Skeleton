namespace Integration.Tests.Support;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Gma.Framework.Cqrs;
using Gma.Framework.Tasks;
using Gma.Framework.Tasks.Cqrs;
using Gma.Framework.Tasks.Infrastructure;
using Gma.Framework.Results;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.Runtime.Time;
using Gma.Modules.TaskRuntime.Application;
using Gma.Modules.TaskRuntime.Application.Commands;
using Gma.Modules.TaskRuntime.Application.Queries;
using Gma.Modules.TaskRuntime.Persistence;
using Gma.Framework.Tenancy.Infrastructure;
using Gma.Framework.Tenancy.Tasks;
using TaskSamples.Application;
using TaskSamples.Application.Tasks;
using TaskSamples.Contracts;

internal sealed class TaskRuntimeTestApplication : IAsyncDisposable
{
    private readonly string provider;
    private readonly string connectionString;
    private readonly IHost host;

    public TaskRuntimeTestApplication(
        string provider,
        string connectionString,
        bool workerEnabled,
        DateTimeOffset? clockUtcNow = null)
    {
        this.provider = provider;
        this.connectionString = connectionString;
        this.Sink = new RecordingTaskSampleReportSink();

        HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Integration",
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Persistence:Provider"] = provider,
            ["ConnectionStrings:SqlServer"] = provider == "SqlServer" ? connectionString : string.Empty,
            ["ConnectionStrings:PostgreSql"] = provider == "PostgreSql" ? connectionString : string.Empty,
            ["Tenancy:Enabled"] = "true",
            ["Tasks:Worker:Enabled"] = workerEnabled.ToString(),
            ["Tasks:Worker:WorkerGroups:0"] = TaskSamplesModuleMetadata.WorkerGroup,
            ["Tasks:Worker:BatchSize"] = "5",
            ["Tasks:Worker:PollInterval"] = "00:00:00.100",
            ["Tasks:Worker:LeaseDuration"] = "00:00:05",
            ["Tasks:Worker:HandlerTimeout"] = "00:00:05",
            ["Tasks:Worker:RetryBaseDelay"] = "00:00:00.100",
            ["Tasks:Worker:RetryMaxDelay"] = "00:00:01",
            ["Tasks:Worker:WorkerId"] = "worker-test",
            ["Tasks:Worker:NodeId"] = "node-test",
        });
        builder.Logging.ClearProviders();
        if (clockUtcNow is not null)
        {
            builder.Services.AddSingleton<ISystemClock>(new FixedClock(clockUtcNow.Value));
        }

        builder.Services.AddTaskRuntimeApplication();
        builder.AddTenancyInfrastructure();
        builder.AddTenantTaskExecutionContext();
        builder.AddTaskRuntimePersistence();
        builder.AddTaskCqrs();
        builder.AddTaskWorkerRuntime();
        builder.Services.AddSingleton(this.Sink);
        builder.Services.AddSingleton<ITaskSampleReportSink>(this.Sink);
        builder.Services.AddTaskSamplesApplication();
        if (workerEnabled)
        {
            builder.Services.AddTaskSamplesTaskHandlers();
        }

        this.host = builder.Build();
    }

    public RecordingTaskSampleReportSink Sink { get; }

    public IServiceProvider Services => this.host.Services;

    public Task StartAsync() => this.host.StartAsync();

    public Task StopAsync() => this.host.StopAsync();

    public async Task MigrateDatabaseAsync()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:Provider"] = this.provider,
                ["ConnectionStrings:SqlServer"] = this.provider == "SqlServer" ? this.connectionString : string.Empty,
                ["ConnectionStrings:PostgreSql"] = this.provider == "PostgreSql" ? this.connectionString : string.Empty,
            })
            .Build();
        DbContextOptionsBuilder<TaskRuntimeDbContext> options = new();
        options.UseConfiguredProvider(
            configuration,
            TaskRuntimeMigrations.SqlServerAssembly,
            TaskRuntimeMigrations.PostgreSqlAssembly,
            TaskRuntimeMigrations.Schema,
            TaskRuntimeMigrations.HistoryTable);

        await using TaskRuntimeDbContext dbContext = new(options.Options);
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);
    }

    public async Task EnqueueSampleTaskAsync(
        Guid runId,
        DateTimeOffset createdAtUtc,
        int maxAttempts = 3,
        int payloadVersion = GenerateReportTaskPayload.PayloadVersion,
        string? deduplicationKey = null,
        string? payloadJson = null,
        string taskName = GenerateReportTaskPayload.TaskName)
    {
        using IServiceScope scope = this.Services.CreateScope();
        ITaskRunStore store = scope.ServiceProvider.GetRequiredService<ITaskRunStore>();
        string payload = payloadJson ?? JsonSerializer.Serialize(new GenerateReportTaskPayload("daily", 10));

        await store.EnqueueAsync(
                new TaskRunRequest(
                    runId,
                    TaskSamplesModuleMetadata.Name,
                    taskName,
                    payload,
                    createdAtUtc,
                    createdAtUtc,
                    TaskSamplesModuleMetadata.WorkerGroup,
                    scopeId: "tenant-a",
                    requestedBy: "operator",
                    maxAttempts: maxAttempts,
                    payloadVersion: payloadVersion,
                    deduplicationKey: deduplicationKey),
                CancellationToken.None)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TaskRunLease>> ClaimAsync(
        string workerId,
        string nodeId,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration)
    {
        using IServiceScope scope = this.Services.CreateScope();
        ITaskRunStore store = scope.ServiceProvider.GetRequiredService<ITaskRunStore>();

        return await store.ClaimReadyAsync(
                new TaskWorkerClaim(
                    TaskSamplesModuleMetadata.WorkerGroup,
                    workerId,
                    nodeId,
                    nowUtc,
                    maxRuns: 10,
                    leaseDuration),
                CancellationToken.None)
            .ConfigureAwait(false);
    }

    public async Task MarkStartedAsync(TaskExecutionContext context, DateTimeOffset nowUtc)
    {
        using IServiceScope scope = this.Services.CreateScope();
        ITaskRunStore store = scope.ServiceProvider.GetRequiredService<ITaskRunStore>();
        await store.MarkStartedAsync(context, nowUtc, CancellationToken.None).ConfigureAwait(false);
    }

    public async Task MarkSucceededAsync(TaskExecutionContext context, DateTimeOffset nowUtc)
    {
        using IServiceScope scope = this.Services.CreateScope();
        ITaskRunStore store = scope.ServiceProvider.GetRequiredService<ITaskRunStore>();
        await store.MarkSucceededAsync(context, nowUtc, CancellationToken.None).ConfigureAwait(false);
    }

    public async Task MarkCanceledAsync(TaskExecutionContext context, DateTimeOffset nowUtc)
    {
        using IServiceScope scope = this.Services.CreateScope();
        ITaskRunStore store = scope.ServiceProvider.GetRequiredService<ITaskRunStore>();
        await store.MarkCanceledAsync(context, nowUtc, CancellationToken.None).ConfigureAwait(false);
    }

    public async Task MarkFailedAsync(
        TaskExecutionContext context,
        string error,
        DateTimeOffset failedAtUtc,
        DateTimeOffset? retryAtUtc)
    {
        using IServiceScope scope = this.Services.CreateScope();
        ITaskRunStore store = scope.ServiceProvider.GetRequiredService<ITaskRunStore>();
        await store.MarkFailedAsync(context, error, failedAtUtc, retryAtUtc, CancellationToken.None).ConfigureAwait(false);
    }

    public async Task ReportProgressAsync(TaskExecutionContext context, TaskProgress progress, DateTimeOffset nowUtc)
    {
        using IServiceScope scope = this.Services.CreateScope();
        ITaskRunStore store = scope.ServiceProvider.GetRequiredService<ITaskRunStore>();
        await store.ReportProgressAsync(context, progress, nowUtc, CancellationToken.None).ConfigureAwait(false);
    }

    public async Task RequestCancellationAsync(Guid runId, string? requestedBy, DateTimeOffset requestedAtUtc)
    {
        using IServiceScope scope = this.Services.CreateScope();
        ITaskRunStore store = scope.ServiceProvider.GetRequiredService<ITaskRunStore>();
        await store.RequestCancellationAsync(runId, requestedBy, requestedAtUtc, CancellationToken.None).ConfigureAwait(false);
    }

    public async Task RetryAsync(Guid runId, string? requestedBy, DateTimeOffset scheduledAtUtc)
    {
        using IServiceScope scope = this.Services.CreateScope();
        ITaskRunStore store = scope.ServiceProvider.GetRequiredService<ITaskRunStore>();
        await store.RetryAsync(runId, requestedBy, scheduledAtUtc, CancellationToken.None).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TaskRunSummary>> MarkStaleTimedOutAsync(
        DateTimeOffset nowUtc,
        TimeSpan staleAfter,
        int maxRuns = 100)
    {
        using IServiceScope scope = this.Services.CreateScope();
        ITaskRunStore store = scope.ServiceProvider.GetRequiredService<ITaskRunStore>();
        return await store.MarkStaleTimedOutAsync(nowUtc, staleAfter, maxRuns, CancellationToken.None)
            .ConfigureAwait(false);
    }

    public async Task<Result<TaskRunDetails>> EnqueueThroughApplicationAsync(
        Guid runId,
        string payloadJson,
        DateTimeOffset scheduledAtUtc,
        string? deduplicationKey = null,
        int payloadVersion = GenerateReportTaskPayload.PayloadVersion)
    {
        using IServiceScope scope = this.Services.CreateScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

        return await dispatcher.SendAsync(
                new EnqueueTaskRunCommand(
                    runId,
                    TaskSamplesModuleMetadata.Name,
                    GenerateReportTaskPayload.TaskName,
                    payloadJson,
                    scheduledAtUtc,
                    TaskSamplesModuleMetadata.WorkerGroup,
                    "tenant-a",
                    null,
                    "operator",
                    3,
                    payloadVersion,
                    deduplicationKey),
                CancellationToken.None)
            .ConfigureAwait(false);
    }

    public async Task<Result<Unit>> CancelThroughApplicationAsync(Guid runId, string? requestedBy)
    {
        using IServiceScope scope = this.Services.CreateScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
        return await dispatcher.SendAsync(new CancelTaskRunCommand(runId, requestedBy), CancellationToken.None)
            .ConfigureAwait(false);
    }

    public async Task<Result<Unit>> RetryThroughApplicationAsync(
        Guid runId,
        string? requestedBy,
        DateTimeOffset scheduledAtUtc)
    {
        using IServiceScope scope = this.Services.CreateScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
        return await dispatcher.SendAsync(
                new RetryTaskRunCommand(runId, requestedBy, scheduledAtUtc),
                CancellationToken.None)
            .ConfigureAwait(false);
    }

    public async Task<Result<IReadOnlyList<TaskRunSummary>>> ListThroughApplicationAsync(
        string? deduplicationKey = null)
    {
        using IServiceScope scope = this.Services.CreateScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
        Result<IReadOnlyList<TaskRunSummary>> result = await dispatcher.QueryAsync(
                new ListTaskRunsQuery(
                    TaskSamplesModuleMetadata.Name,
                    GenerateReportTaskPayload.TaskName,
                    TaskSamplesModuleMetadata.WorkerGroup,
                    null,
                    "tenant-a",
                    1,
                    50),
                CancellationToken.None)
            .ConfigureAwait(false);

        return result.IsFailure || string.IsNullOrWhiteSpace(deduplicationKey)
            ? result
            : Result.Success<IReadOnlyList<TaskRunSummary>>(
                result.Value.Where(run => run.DeduplicationKey == deduplicationKey).ToArray());
    }

    public async Task<Result<TaskRunStats>> GetStatsThroughApplicationAsync()
    {
        using IServiceScope scope = this.Services.CreateScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
        return await dispatcher.QueryAsync(
                new GetTaskRunStatsQuery(
                    TaskSamplesModuleMetadata.Name,
                    null,
                    TaskSamplesModuleMetadata.WorkerGroup,
                    "tenant-a"),
                CancellationToken.None)
            .ConfigureAwait(false);
    }

    public async Task<Result<TaskControlMessage>> SendControlThroughApplicationAsync(
        Guid runId,
        string commandName,
        string payloadJson,
        DateTimeOffset? expiresAtUtc)
    {
        using IServiceScope scope = this.Services.CreateScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
        return await dispatcher.SendAsync(
                new SendTaskControlMessageCommand(
                    runId,
                    commandName,
                    payloadJson,
                    expiresAtUtc,
                    "operator-control"),
                CancellationToken.None)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TaskControlMessage>> ReadPendingControlAsync(TaskExecutionContext context)
    {
        using IServiceScope scope = this.Services.CreateScope();
        ITaskRunStore store = scope.ServiceProvider.GetRequiredService<ITaskRunStore>();
        return await store.ReadPendingAsync(context, 10, CancellationToken.None).ConfigureAwait(false);
    }

    public async Task EnqueueControlAsync(Guid messageId, Guid runId, DateTimeOffset nowUtc)
    {
        using IServiceScope scope = this.Services.CreateScope();
        ITaskRunStore store = scope.ServiceProvider.GetRequiredService<ITaskRunStore>();
        await store.EnqueueControlMessageAsync(
                new TaskControlMessage(
                    messageId,
                    runId,
                    "tasks.pause",
                    "{}",
                    nowUtc,
                    "operator",
                    nowUtc.AddMinutes(5)),
                CancellationToken.None)
            .ConfigureAwait(false);
    }

    public async Task<TaskRunSnapshot> GetSnapshotAsync(Guid runId)
    {
        using IServiceScope scope = this.Services.CreateScope();
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

        Xunit.Assert.NotNull(snapshot);
        return snapshot;
    }

    public async Task<TaskRunSnapshot> WaitForStatusAsync(
        Guid runId,
        Gma.Framework.Tasks.TaskRunStatus expectedStatus,
        TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            TaskRunSnapshot snapshot = await this.GetSnapshotAsync(runId).ConfigureAwait(false);
            if (snapshot.Status == expectedStatus)
            {
                return snapshot;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
        }

        return await this.GetSnapshotAsync(runId).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await this.host.StopAsync().ConfigureAwait(false);
        this.host.Dispose();
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}

internal sealed class RecordingTaskSampleReportSink : ITaskSampleReportSink
{
    private readonly List<TaskSampleReport> reports = [];

    public IReadOnlyList<TaskSampleReport> Reports
    {
        get
        {
            lock (this.reports)
            {
                return this.reports.ToArray();
            }
        }
    }

    public Task RecordAsync(TaskSampleReport report, CancellationToken cancellationToken)
    {
        lock (this.reports)
        {
            this.reports.Add(report);
        }

        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<TaskSampleReport>> WaitForReportsAsync(int expectedCount, TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            IReadOnlyList<TaskSampleReport> current = this.Reports;
            if (current.Count >= expectedCount)
            {
                return current;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
        }

        return this.Reports;
    }
}
