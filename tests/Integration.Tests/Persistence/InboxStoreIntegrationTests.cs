namespace Integration.Tests;

using Catalog.Contracts;
using DotNet.Testcontainers.Containers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ordering.Persistence;
using Gma.Framework.Messaging;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Runtime.Infrastructure;
using Gma.Framework.Tenancy.Infrastructure;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class InboxStoreIntegrationTests
{
    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Failed_handler_rolls_back_side_effects_and_allows_retry()
    {
        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("gma_inbox_tests")
            .Build();
        await postgreSql.StartAsync();

        await using ServiceProvider provider = BuildProvider(postgreSql.GetConnectionString());
        await MigrateAsync(provider);

        InboxMessageRecord record = new(
            Guid.NewGuid(),
            "catalog-item-created-projection",
            "gma.catalog.item-created.v1",
            "item-created",
            1,
            "tenant-a",
            new DateTimeOffset(2026, 6, 30, 12, 0, 0, TimeSpan.Zero));

        using (IServiceScope scope = provider.CreateScope())
        {
            OrderingDbContext dbContext = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
            IInboxStore store = scope.ServiceProvider.GetServices<IInboxStore>().Single(item => item.ModuleName == "ordering");

            InboxProcessResult failed = await store.ProcessAsync(
                record,
                _ =>
                {
                    dbContext.CatalogItemProjections.Add(CatalogItemProjection.Create(
                        Guid.NewGuid(),
                        "tenant-a",
                        Guid.NewGuid(),
                        "SKU-1",
                        "Leaked projection",
                        10,
                        "USD",
                        CatalogItemStatus.Active));
                    return Task.FromException(new InvalidOperationException("handler failed"));
                },
                CancellationToken.None);

            Assert.Equal(InboxProcessStatus.Failed, failed.Status);
        }

        using (IServiceScope scope = provider.CreateScope())
        {
            OrderingDbContext dbContext = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
            InboxMessage failure = await dbContext.InboxMessages.SingleAsync();

            Assert.Equal(0, await dbContext.CatalogItemProjections.CountAsync());
            Assert.Equal(1, failure.Attempts);
            Assert.Equal("handler failed", failure.LastError);
        }

        Guid committedCatalogItemId = Guid.NewGuid();
        using (IServiceScope scope = provider.CreateScope())
        {
            OrderingDbContext dbContext = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
            IInboxStore store = scope.ServiceProvider.GetServices<IInboxStore>().Single(item => item.ModuleName == "ordering");

            InboxProcessResult retry = await store.ProcessAsync(
                record,
                _ =>
                {
                    dbContext.CatalogItemProjections.Add(CatalogItemProjection.Create(
                        Guid.NewGuid(),
                        "tenant-a",
                        committedCatalogItemId,
                        "SKU-2",
                        "Committed projection",
                        20,
                        "USD",
                        CatalogItemStatus.Active));
                    return Task.CompletedTask;
                },
                CancellationToken.None);

            Assert.Equal(InboxProcessStatus.Processed, retry.Status);
        }

        using (IServiceScope scope = provider.CreateScope())
        {
            OrderingDbContext dbContext = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
            InboxMessage processed = await dbContext.InboxMessages.SingleAsync();

            Assert.Equal(1, await dbContext.CatalogItemProjections.CountAsync());
            Assert.Equal(2, processed.Attempts);
            Assert.Equal(InboxMessageStatus.Processed, processed.Status);
        }
    }

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Handler_cancellation_is_recorded_as_failed_attempt_when_store_is_not_stopping()
    {
        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("gma_inbox_cancellation_tests")
            .Build();
        await postgreSql.StartAsync();

        await using ServiceProvider provider = BuildProvider(postgreSql.GetConnectionString());
        await MigrateAsync(provider);

        InboxMessageRecord record = new(
            Guid.NewGuid(),
            "catalog-item-updated-projection",
            "gma.catalog.item-updated.v1",
            "item-updated",
            1,
            "tenant-a",
            new DateTimeOffset(2026, 6, 30, 12, 0, 0, TimeSpan.Zero));

        using (IServiceScope scope = provider.CreateScope())
        {
            IInboxStore store = scope.ServiceProvider.GetServices<IInboxStore>().Single(item => item.ModuleName == "ordering");
            using CancellationTokenSource handlerCancellation = new();
            handlerCancellation.Cancel();

            InboxProcessResult failed = await store.ProcessAsync(
                record,
                _ => Task.FromCanceled(handlerCancellation.Token),
                CancellationToken.None);

            Assert.Equal(InboxProcessStatus.Failed, failed.Status);
            Assert.Contains("canceled", failed.Error, StringComparison.OrdinalIgnoreCase);
        }

        using (IServiceScope scope = provider.CreateScope())
        {
            OrderingDbContext dbContext = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
            InboxMessage failure = await dbContext.InboxMessages.SingleAsync();

            Assert.Equal(1, failure.Attempts);
            Assert.Equal(InboxMessageStatus.Failed, failure.Status);
            Assert.Contains("canceled", failure.LastError, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static ServiceProvider BuildProvider(string connectionString)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Persistence:Provider"] = "PostgreSql";
        builder.Configuration["ConnectionStrings:PostgreSql"] = connectionString;
        builder.Configuration["Tenancy:Enabled"] = "false";
        builder.AddRuntimeInfrastructure();
        builder.AddTenancyInfrastructure();
        builder.AddOrderingPersistence();

        return builder.Services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static async Task MigrateAsync(ServiceProvider provider)
    {
        using IServiceScope scope = provider.CreateScope();
        OrderingDbContext dbContext = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}
