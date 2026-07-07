namespace Ordering.Tests;

using Catalog.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Gma.Modules.Notifications.Contracts;
using Ordering.Application;
using Ordering.Application.Ports;
using Ordering.Contracts;
using Ordering.Domain.Aggregates;
using Gma.Framework.Messaging;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrderingNotificationIntegrationTests
{
    private static readonly Guid CatalogItemId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly Guid OtherCatalogItemId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
    private static readonly DateTimeOffset Now = new(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Catalog_item_updates_notify_only_users_with_orders_for_the_changed_item()
    {
        RecordingOrderRepository orders = new(
        [
            new("tenant-a", CatalogItemId, "user-2"),
            new("tenant-a", CatalogItemId, "user-1"),
            new("tenant-a", CatalogItemId, "user-1"),
            new("tenant-a", OtherCatalogItemId, "other-item-user"),
            new("tenant-b", CatalogItemId, "other-tenant-user")
        ]);
        RecordingCatalogProjectionRepository projections = new();
        RecordingOutboxWriter outbox = new(OrderingModuleMetadata.Name);
        using ServiceProvider provider = BuildProvider(orders, projections, outbox);
        using IServiceScope scope = provider.CreateScope();
        IntegrationEventSubscription subscription = provider
            .GetRequiredService<IIntegrationEventSubscriptionRegistry>()
            .Subscriptions
            .Single(subscription => subscription.EventType == typeof(CatalogItemUpdatedIntegrationEvent));

        await IntegrationEventHandlerInvoker.InvokeAsync(
            scope.ServiceProvider,
            subscription,
            new CatalogItemUpdatedIntegrationEvent(
                Guid.Parse("cccccccc-dddd-eeee-ffff-000000000000"),
                "tenant-a",
                Now,
                CatalogItemId,
                "sku-1",
                "Updated item",
                12m,
                "USD",
                CatalogItemStatus.Active,
                ["US"]),
            CancellationToken.None);

        CatalogItemProjectionWriteModel projection = Assert.Single(projections.Upserts);
        Assert.Equal(CatalogItemId, projection.CatalogItemId);
        Assert.Equal(["US"], projection.AvailableRegions);
        Assert.Equal(["user-1", "user-2"], outbox.Events
            .OfType<UserNotificationRequestedIntegrationEvent>()
            .Select(integrationEvent => integrationEvent.UserId)
            .Order(StringComparer.Ordinal)
            .ToArray());
        Assert.All(
            outbox.Events.OfType<UserNotificationRequestedIntegrationEvent>(),
            integrationEvent =>
            {
                Assert.Equal("tenant-a", integrationEvent.TenantId);
                Assert.Equal(OrderingModuleMetadata.Name, integrationEvent.SourceModule);
                Assert.Equal(OrderingNotificationNames.CatalogItemChanged, integrationEvent.NotificationName);
                Assert.Equal(OrderingNotificationNames.CatalogItemChangedVersion, integrationEvent.NotificationVersion);
            });
    }

    private static ServiceProvider BuildProvider(
        RecordingOrderRepository orders,
        RecordingCatalogProjectionRepository projections,
        RecordingOutboxWriter outbox)
    {
        ServiceCollection services = new();
        services.AddOrderingApplication();
        services.AddScoped<IOrderRepository>(_ => orders);
        services.AddScoped<ICatalogItemProjectionRepository>(_ => projections);
        services.AddSingleton<IOutboxWriterRegistry>(new RecordingOutboxWriterRegistry(outbox));
        services.AddSingleton<IIdGenerator>(new SequenceIdGenerator());
        services.AddSingleton<ISystemClock>(new FixedClock(Now));

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = false,
            ValidateScopes = true
        });
    }

    private sealed record OrderOwner(string TenantId, Guid CatalogItemId, string UserId);

    private sealed class RecordingOrderRepository(IReadOnlyCollection<OrderOwner> owners) : IOrderRepository
    {
        public Task AddAsync(Order order, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyCollection<string>> ListDistinctUserIdsByCatalogItemAsync(
            string tenantId,
            Guid catalogItemId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<string>>(owners
                .Where(owner =>
                    owner.TenantId == tenantId &&
                    owner.CatalogItemId == catalogItemId)
                .Select(owner => owner.UserId)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray());
    }

    private sealed class RecordingCatalogProjectionRepository : ICatalogItemProjectionRepository
    {
        public List<CatalogItemProjectionWriteModel> Upserts { get; } = [];

        public Task<CatalogItemProjectionSnapshot?> GetAsync(Guid catalogItemId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task UpsertAsync(CatalogItemProjectionWriteModel item, CancellationToken cancellationToken)
        {
            this.Upserts.Add(item);
            return Task.CompletedTask;
        }

        public Task MarkDiscontinuedAsync(string tenantId, Guid catalogItemId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingOutboxWriterRegistry(RecordingOutboxWriter writer) : IOutboxWriterRegistry
    {
        public IOutboxWriter GetRequired(string moduleName) =>
            string.Equals(moduleName, writer.ModuleName, StringComparison.Ordinal)
                ? writer
                : throw new InvalidOperationException($"No outbox writer is registered for module '{moduleName}'.");
    }

    private sealed class RecordingOutboxWriter(string moduleName) : IOutboxWriter
    {
        public string ModuleName { get; } = moduleName;
        public List<IIntegrationEvent> Events { get; } = [];

        public Task EnqueueAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
            where TEvent : IIntegrationEvent
        {
            this.Events.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class SequenceIdGenerator : IIdGenerator
    {
        private int value;

        public Guid NewId()
        {
            this.value++;
            return Guid.Parse($"00000000-0000-0000-0000-{this.value:000000000000}");
        }
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
