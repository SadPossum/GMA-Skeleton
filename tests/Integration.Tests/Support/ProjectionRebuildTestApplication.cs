namespace Integration.Tests.Support;

using System.Text.Json;
using Catalog.Application;
using Catalog.Contracts;
using Catalog.Domain.Aggregates;
using Catalog.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ordering.Application;
using Ordering.Application.Tasks;
using Ordering.Contracts;
using Ordering.Persistence;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Infrastructure;
using Gma.Framework.Tasks;
using Gma.Framework.Tasks.Infrastructure;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Infrastructure;
using Gma.Framework.Tenancy.Scoping;
using Gma.Framework.Tenancy.Tasks;
using Gma.Modules.TaskRuntime.Application;
using Gma.Modules.TaskRuntime.Persistence;

internal sealed class ProjectionRebuildTestApplication : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHost host;

    public ProjectionRebuildTestApplication(string provider, string connectionString)
    {
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
            ["Tasks:Worker:Enabled"] = "true",
            ["Tasks:Worker:WorkerGroups:0"] = OrderingModuleMetadata.ProjectionWorkerGroup,
            ["Tasks:Worker:BatchSize"] = "5",
            ["Tasks:Worker:PollInterval"] = "00:00:00.100",
            ["Tasks:Worker:LeaseDuration"] = "00:00:10",
            ["Tasks:Worker:HandlerTimeout"] = "00:00:20",
            ["Tasks:Worker:RetryBaseDelay"] = "00:00:00.100",
            ["Tasks:Worker:RetryMaxDelay"] = "00:00:01",
            ["Tasks:Worker:WorkerId"] = "projection-rebuild-worker",
            ["Tasks:Worker:NodeId"] = "projection-rebuild-node",
        });
        builder.Logging.ClearProviders();

        builder.AddRuntimeInfrastructure();
        builder.AddTenancyInfrastructure();
        builder.AddTenantScoping();
        builder.AddTenantTaskExecutionContext();
        builder.Services.AddTaskRuntimeApplication();
        builder.AddTaskRuntimePersistence();
        builder.AddTaskWorkerRuntime();
        builder.Services.AddCatalogApplication();
        builder.AddCatalogPersistence();
        builder.Services.AddOrderingApplication();
        builder.AddOrderingPersistence();

        this.host = builder.Build();
    }

    public IServiceProvider Services => this.host.Services;

    public Task StartAsync() => this.host.StartAsync();

    public async Task MigrateDatabaseAsync()
    {
        using IServiceScope scope = this.Services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<TaskRuntimeDbContext>()
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
    }

    public async Task AddCatalogItemAsync(
        string scopeId,
        Guid itemId,
        string sku,
        string name,
        decimal price,
        string currency)
    {
        using IServiceScope scope = this.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>().SetTenant(scopeId);
        CatalogDbContext dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        Result<CatalogItem> result = CatalogItem.Create(
            itemId,
            scopeId,
            sku,
            name,
            price,
            currency,
            availableRegions: null,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error.Code);
        }

        dbContext.CatalogItems.Add(result.Value);
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task EnqueueOrderingProjectionRebuildAsync(
        Guid runId,
        string scopeId,
        int batchSize,
        bool dryRun = false,
        string? cursor = null)
    {
        using IServiceScope scope = this.Services.CreateScope();
        ITaskRunStore store = scope.ServiceProvider.GetRequiredService<ITaskRunStore>();
        string payload = JsonSerializer.Serialize(
            new RebuildCatalogItemProjectionPayload(
                OrderingModuleMetadata.CatalogItemProjectionVersion,
                batchSize,
                dryRun,
                cursor),
            JsonOptions);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await store.EnqueueAsync(
                new TaskRunRequest(
                    runId,
                    OrderingModuleMetadata.Name,
                    RebuildCatalogItemProjectionPayload.TaskName,
                    payload,
                    now,
                    now,
                    OrderingModuleMetadata.ProjectionWorkerGroup,
                    scopeId,
                    requestedBy: "integration-test",
                    maxAttempts: 2,
                    payloadVersion: RebuildCatalogItemProjectionPayload.PayloadVersion,
                    deduplicationKey: null),
                CancellationToken.None)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OrderingCatalogProjectionSnapshot>> GetOrderingProjectionsAsync(string scopeId)
    {
        using IServiceScope scope = this.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>().SetTenant(scopeId);
        OrderingDbContext dbContext = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();

        return await dbContext.CatalogItemProjections
            .OrderBy(item => item.Sku)
            .Select(item => new OrderingCatalogProjectionSnapshot(
                item.ScopeId,
                item.CatalogItemId,
                item.Sku,
                item.Name,
                item.Price,
                item.Currency,
                item.Status))
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task<ProjectionRebuildCheckpointSnapshot?> GetCheckpointAsync(string scopeId, Guid runId)
    {
        using IServiceScope scope = this.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>().SetTenant(scopeId);
        OrderingDbContext dbContext = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();

        return await dbContext.ProjectionRebuildCheckpoints
            .Where(checkpoint =>
                checkpoint.RunId == runId &&
                checkpoint.ProjectionName == OrderingModuleMetadata.CatalogItemProjectionName)
            .Select(checkpoint => new ProjectionRebuildCheckpointSnapshot(
                checkpoint.RunId,
                checkpoint.ScopeId,
                checkpoint.ProjectionName,
                checkpoint.Cursor,
                checkpoint.ProcessedCount,
                checkpoint.WrittenCount,
                checkpoint.SkippedCount,
                checkpoint.FailedCount,
                checkpoint.ProjectionVersion,
                checkpoint.CompletedAtUtc))
            .SingleOrDefaultAsync()
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
        TaskRunStatus expectedStatus,
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
}

internal sealed record OrderingCatalogProjectionSnapshot(
    string ScopeId,
    Guid CatalogItemId,
    string Sku,
    string Name,
    decimal Price,
    string Currency,
    CatalogItemStatus Status);

internal sealed record ProjectionRebuildCheckpointSnapshot(
    Guid RunId,
    string ScopeId,
    string ProjectionName,
    string? Cursor,
    long ProcessedCount,
    long WrittenCount,
    long SkippedCount,
    long FailedCount,
    int ProjectionVersion,
    DateTimeOffset? CompletedAtUtc);
