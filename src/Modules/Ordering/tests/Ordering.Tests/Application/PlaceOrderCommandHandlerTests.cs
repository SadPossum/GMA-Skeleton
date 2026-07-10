namespace Ordering.Tests;

using Gma.Framework.Naming;
using Catalog.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ordering.Application;
using Ordering.Application.Commands;
using Ordering.Application.Ports;
using Ordering.Contracts;
using Ordering.Domain.Aggregates;
using Ordering.Domain.Errors;
using Gma.Framework.AccessControl;
using Gma.Framework.Cqrs;
using Gma.Framework.Messaging;
using Gma.Framework.Scoping;
using Gma.Framework.Numerics;
using Gma.Framework.Results;
using Gma.Framework.Cqrs.Infrastructure;
using Gma.Framework.Runtime.Infrastructure;
using Xunit;

[Trait("Category", "Unit")]
public sealed class PlaceOrderCommandHandlerTests
{
    [Fact]
    public void Ordering_application_registration_is_idempotent()
    {
        ServiceCollection services = new();

        services.AddOrderingApplication();
        services.AddOrderingApplication();

        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ICommandHandler<PlaceOrderCommand, OrderDto>));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ICommandValidator<PlaceOrderCommand>));
        Assert.Equal(3, services.Count(descriptor => descriptor.ServiceType == typeof(IntegrationEventSubscription)));

        using ServiceProvider provider = services.BuildServiceProvider();
        IIntegrationEventSubscriptionRegistry registry =
            provider.GetRequiredService<IIntegrationEventSubscriptionRegistry>();

        Assert.Equal(3, registry.Subscriptions.Count);
    }

    [Fact]
    public async Task Place_order_rejects_unknown_catalog_projection_status()
    {
        Guid catalogItemId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        RecordingOrderRepository orderRepository = new();
        CatalogProjectionRepository catalogRepository = new(new CatalogItemProjectionSnapshot(
            catalogItemId,
            "SKU-1",
            "Unknown status item",
            10m,
            "USD",
            CatalogItemStatus.Unknown));
        using IHost host = BuildHost(orderRepository, catalogRepository);
        using IServiceScope scope = host.Services.CreateScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

        Result<OrderDto> result = await dispatcher
            .SendAsync(new PlaceOrderCommand(catalogItemId, 1, UserSubject(), "US"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(OrderingDomainErrors.CatalogItemStatusUnknown, result.Error);
        Assert.Empty(orderRepository.Orders);
    }

    [Fact]
    public async Task Place_order_rejects_invalid_catalog_projection_data()
    {
        Guid catalogItemId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        RecordingOrderRepository orderRepository = new();
        CatalogProjectionRepository catalogRepository = new(new CatalogItemProjectionSnapshot(
            catalogItemId,
            "SKU-1",
            "Bad price item",
            0,
            "USD",
            CatalogItemStatus.Active));
        using IHost host = BuildHost(orderRepository, catalogRepository);
        using IServiceScope scope = host.Services.CreateScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

        Result<OrderDto> result = await dispatcher
            .SendAsync(new PlaceOrderCommand(catalogItemId, 1, UserSubject(), "US"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(OrderingDomainErrors.CatalogItemPriceMustBePositive, result.Error);
        Assert.Empty(orderRepository.Orders);
    }

    [Fact]
    public void Order_create_normalizes_and_rejects_invalid_tenant_id()
    {
        Result<Order> normalized = CreateOrder(" tenant-a ");
        Result<Order> missing = CreateOrder(" ");
        Result<Order> invalid = CreateOrder(new string('x', ScopeIds.MaxLength + 1));

        Assert.True(normalized.IsSuccess);
        Assert.Equal("tenant-a", normalized.Value.ScopeId);
        Assert.True(missing.IsFailure);
        Assert.Equal(OrderingDomainErrors.TenantRequired, missing.Error);
        Assert.True(invalid.IsFailure);
        Assert.Equal(OrderingDomainErrors.TenantInvalid, invalid.Error);
    }

    [Fact]
    public void Order_create_rejects_empty_order_id()
    {
        Result<Order> result = CreateOrder("tenant-a", orderId: Guid.Empty);

        Assert.True(result.IsFailure);
        Assert.Equal(OrderingDomainErrors.OrderIdRequired, result.Error);
    }

    [Fact]
    public void Order_create_normalizes_catalog_snapshot_data()
    {
        Result<Order> result = CreateOrder(
            "tenant-a",
            userId: " user-1 ",
            catalogSku: " sku-1 ",
            catalogItemName: " Catalog item ",
            regionCode: " us ",
            currency: " usd ");

        Assert.True(result.IsSuccess);
        Assert.Equal("user-1", result.Value.UserId);
        Assert.Equal("SKU-1", result.Value.CatalogSku);
        Assert.Equal("Catalog item", result.Value.CatalogItemName);
        Assert.Equal("USD", result.Value.Currency);
        Assert.Equal("US", result.Value.RegionCode);
    }

    [Fact]
    public void Order_create_rejects_invalid_catalog_snapshot_data()
    {
        Assert.Equal(OrderingDomainErrors.CatalogItemRequired, CreateOrder("tenant-a", catalogItemId: Guid.Empty).Error);
        Assert.Equal(OrderingDomainErrors.CatalogSkuRequired, CreateOrder("tenant-a", catalogSku: " ").Error);
        Assert.Equal(
            OrderingDomainErrors.CatalogSkuTooLong,
            CreateOrder("tenant-a", catalogSku: new string('x', Order.CatalogSkuMaxLength + 1)).Error);
        Assert.Equal(OrderingDomainErrors.CatalogItemNameRequired, CreateOrder("tenant-a", catalogItemName: " ").Error);
        Assert.Equal(
            OrderingDomainErrors.CatalogItemNameTooLong,
            CreateOrder("tenant-a", catalogItemName: new string('x', Order.CatalogItemNameMaxLength + 1)).Error);
        Assert.Equal(OrderingDomainErrors.CatalogItemPriceMustBePositive, CreateOrder("tenant-a", unitPrice: 0).Error);
        Assert.Equal(OrderingDomainErrors.CatalogItemPriceNotSupported, CreateOrder("tenant-a", unitPrice: 10.123m).Error);
        Assert.Equal(
            OrderingDomainErrors.CatalogItemPriceNotSupported,
            CreateOrder(
                "tenant-a",
                unitPrice: DecimalPrecision.MaxValue(Order.AmountPrecision, Order.AmountScale) + 0.01m).Error);
        Assert.Equal(OrderingDomainErrors.CatalogItemCurrencyInvalid, CreateOrder("tenant-a", currency: "US").Error);
        Assert.Equal(
            OrderingDomainErrors.OrderTotalNotSupported,
            CreateOrder(
                "tenant-a",
                unitPrice: DecimalPrecision.MaxValue(Order.AmountPrecision, Order.AmountScale),
                quantity: 2).Error);
    }

    [Fact]
    public void Order_create_rejects_invalid_user_and_region()
    {
        Assert.Equal(OrderingDomainErrors.UserIdRequired, CreateOrder("tenant-a", userId: " ").Error);
        Assert.Equal(
            OrderingDomainErrors.UserIdInvalid,
            CreateOrder("tenant-a", userId: new string('x', Order.UserIdMaxLength + 1)).Error);
        Assert.Equal(OrderingDomainErrors.RegionInvalid, CreateOrder("tenant-a", regionCode: " ").Error);
        Assert.Equal(OrderingDomainErrors.RegionInvalid, CreateOrder("tenant-a", regionCode: "-us").Error);
    }

    [Fact]
    public async Task Place_order_rejects_catalog_item_unavailable_in_user_region()
    {
        Guid catalogItemId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        RecordingOrderRepository orderRepository = new();
        CatalogProjectionRepository catalogRepository = new(new CatalogItemProjectionSnapshot(
            catalogItemId,
            "SKU-1",
            "Region item",
            10m,
            "USD",
            CatalogItemStatus.Active,
            ["EU"]));
        using IHost host = BuildHost(orderRepository, catalogRepository);
        using IServiceScope scope = host.Services.CreateScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

        Result<OrderDto> result = await dispatcher
            .SendAsync(new PlaceOrderCommand(catalogItemId, 1, UserSubject(), "US"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(OrderingDomainErrors.CatalogItemUnavailableInRegion, result.Error);
        Assert.Empty(orderRepository.Orders);
    }

    private static Result<Order> CreateOrder(
        string scopeId,
        Guid? orderId = null,
        string userId = "user-1",
        Guid? catalogItemId = null,
        string catalogSku = "SKU-1",
        string catalogItemName = "Catalog item",
        decimal unitPrice = 10m,
        string currency = "USD",
        string regionCode = "US",
        int quantity = 1) =>
        Order.Create(
            orderId ?? Guid.NewGuid(),
            scopeId,
            userId,
            catalogItemId ?? Guid.NewGuid(),
            catalogSku,
            catalogItemName,
            unitPrice,
            currency,
            regionCode,
            quantity,
            DateTimeOffset.UtcNow);

    private static AccessSubject UserSubject() => AccessSubject.User("user-1");

    private static IHost BuildHost(
        RecordingOrderRepository orderRepository,
        CatalogProjectionRepository catalogRepository)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        TestScopeContext scopeContext = new("tenant-a");

        builder.AddRuntimeInfrastructure();
        builder.AddCqrsInfrastructure();
        builder.Services.AddOrderingApplication();
        builder.Services.AddSingleton<IScopeContext>(scopeContext);
        builder.Services.AddSingleton<IScopeContextAccessor>(scopeContext);
        builder.Services.AddScoped<IOrderRepository>(_ => orderRepository);
        builder.Services.AddScoped<ICatalogItemProjectionRepository>(_ => catalogRepository);

        return builder.Build();
    }

    private sealed class TestScopeContext(string scopeId) : IScopeContextAccessor
    {
        public bool IsEnabled => true;
        public string? ScopeId { get; private set; } = scopeId;

        public void SetScope(string scopeId) => this.ScopeId = scopeId;

        public void ClearScope() => this.ScopeId = null;
    }

    private sealed class RecordingOrderRepository : IOrderRepository
    {
        public List<Order> Orders { get; } = [];

        public Task AddAsync(Order order, CancellationToken cancellationToken)
        {
            this.Orders.Add(order);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<string>> ListDistinctUserIdsByCatalogItemAsync(
            string scopeId,
            Guid catalogItemId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<string>>(
                this.Orders
                    .Where(order => order.ScopeId == scopeId && order.CatalogItemId == catalogItemId)
                    .Select(order => order.UserId)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray());
    }

    private sealed class CatalogProjectionRepository(CatalogItemProjectionSnapshot? projection)
        : ICatalogItemProjectionRepository
    {
        public Task<CatalogItemProjectionSnapshot?> GetAsync(Guid catalogItemId, CancellationToken cancellationToken) =>
            Task.FromResult(projection?.CatalogItemId == catalogItemId ? projection : null);

        public Task UpsertAsync(CatalogItemProjectionWriteModel item, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task MarkDiscontinuedAsync(string scopeId, Guid catalogItemId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
