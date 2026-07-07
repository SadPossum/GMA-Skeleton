namespace Integration.Tests;

using Catalog.Contracts;
using Integration.Tests.Support;
using Ordering.Contracts;
using Gma.Framework.Tasks;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class ProjectionRebuildTaskIntegrationTests
{
    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Ordering_projection_rebuild_task_replays_catalog_exports_into_local_projection_for_each_provider()
    {
        await RunScenarioAsync(
            "SqlServer",
            async () =>
            {
                MsSqlContainer sqlServer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
                await sqlServer.StartAsync();
                return new ProviderLease(
                    sqlServer,
                    AuthTestContainers.UseDatabase(sqlServer.GetConnectionString(), "gma_projection_rebuild_tests"));
            });

        await RunScenarioAsync(
            "PostgreSql",
            async () =>
            {
                PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
                    .WithDatabase("gma_projection_rebuild_tests")
                    .Build();
                await postgreSql.StartAsync();
                return new ProviderLease(postgreSql, postgreSql.GetConnectionString());
            });
    }

    private static async Task RunScenarioAsync(
        string provider,
        Func<Task<ProviderLease>> createProvider)
    {
        await using ProviderLease providerLease = await createProvider().ConfigureAwait(false);
        await using ProjectionRebuildTestApplication application = new(provider, providerLease.ConnectionString);
        await application.MigrateDatabaseAsync().ConfigureAwait(false);

        Guid tenantAItemOne = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
        Guid tenantAItemTwo = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2");
        Guid tenantBItem = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1");
        await application.AddCatalogItemAsync("tenant-a", tenantAItemTwo, "SKU-002", "Second item", 20m, "USD")
            .ConfigureAwait(false);
        await application.AddCatalogItemAsync("tenant-a", tenantAItemOne, "SKU-001", "First item", 10m, "USD")
            .ConfigureAwait(false);
        await application.AddCatalogItemAsync("tenant-b", tenantBItem, "SKU-999", "Other tenant item", 30m, "USD")
            .ConfigureAwait(false);

        Guid runId = provider == "SqlServer"
            ? Guid.Parse("cccccccc-cccc-cccc-cccc-111111111111")
            : Guid.Parse("cccccccc-cccc-cccc-cccc-222222222222");
        await application.EnqueueOrderingProjectionRebuildAsync(
                runId,
                "tenant-a",
                batchSize: 1)
            .ConfigureAwait(false);
        await application.StartAsync().ConfigureAwait(false);

        TaskRunSnapshot run = await application.WaitForStatusAsync(
                runId,
                TaskRunStatus.Succeeded,
                TimeSpan.FromSeconds(20))
            .ConfigureAwait(false);
        IReadOnlyList<OrderingCatalogProjectionSnapshot> tenantAProjections =
            await application.GetOrderingProjectionsAsync("tenant-a").ConfigureAwait(false);
        IReadOnlyList<OrderingCatalogProjectionSnapshot> tenantBProjections =
            await application.GetOrderingProjectionsAsync("tenant-b").ConfigureAwait(false);
        ProjectionRebuildCheckpointSnapshot? checkpoint =
            await application.GetCheckpointAsync("tenant-a", runId).ConfigureAwait(false);

        Assert.Equal(TaskRunStatus.Succeeded, run.Status);
        Assert.Equal(1, run.Attempts);
        Assert.Equal(100, run.ProgressPercent);
        Assert.Contains("processed=2", run.ProgressMessage, StringComparison.Ordinal);
        Assert.Contains("written=2", run.ProgressMessage, StringComparison.Ordinal);
        Assert.Collection(
            tenantAProjections,
            item =>
            {
                Assert.Equal("tenant-a", item.TenantId);
                Assert.Equal(tenantAItemOne, item.CatalogItemId);
                Assert.Equal("SKU-001", item.Sku);
                Assert.Equal("First item", item.Name);
                Assert.Equal(10m, item.Price);
                Assert.Equal("USD", item.Currency);
                Assert.Equal(CatalogItemStatus.Active, item.Status);
            },
            item =>
            {
                Assert.Equal("tenant-a", item.TenantId);
                Assert.Equal(tenantAItemTwo, item.CatalogItemId);
                Assert.Equal("SKU-002", item.Sku);
                Assert.Equal("Second item", item.Name);
                Assert.Equal(20m, item.Price);
                Assert.Equal("USD", item.Currency);
                Assert.Equal(CatalogItemStatus.Active, item.Status);
            });
        Assert.Empty(tenantBProjections);
        Assert.NotNull(checkpoint);
        Assert.Equal(runId, checkpoint.RunId);
        Assert.Equal("tenant-a", checkpoint.TenantId);
        Assert.Equal(OrderingModuleMetadata.CatalogItemProjectionName, checkpoint.ProjectionName);
        Assert.Equal("SKU-002", checkpoint.Cursor);
        Assert.Equal(2, checkpoint.ProcessedCount);
        Assert.Equal(2, checkpoint.WrittenCount);
        Assert.Equal(0, checkpoint.SkippedCount);
        Assert.Equal(0, checkpoint.FailedCount);
        Assert.Equal(OrderingModuleMetadata.CatalogItemProjectionVersion, checkpoint.ProjectionVersion);
        Assert.NotNull(checkpoint.CompletedAtUtc);
    }
}
