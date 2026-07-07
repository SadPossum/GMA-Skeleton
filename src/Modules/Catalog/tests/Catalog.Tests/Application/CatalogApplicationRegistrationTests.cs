namespace Catalog.Tests;

using Catalog.Application;
using Catalog.Application.Commands;
using Catalog.Application.Queries;
using Catalog.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Gma.Framework.Cqrs;
using Gma.Framework.Application.Events;
using Xunit;

[Trait("Category", "Unit")]
public sealed class CatalogApplicationRegistrationTests
{
    [Fact]
    public void Catalog_application_registration_is_idempotent()
    {
        ServiceCollection services = new();

        services.AddCatalogApplication();
        services.AddCatalogApplication();

        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ICommandHandler<CreateCatalogItemCommand, CatalogItemDto>));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ICommandHandler<UpdateCatalogItemCommand, CatalogItemDto>));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ICommandHandler<DiscontinueCatalogItemCommand, Unit>));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IQueryHandler<GetCatalogItemQuery, CatalogItemDto>));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IQueryHandler<ListCatalogItemsQuery, CatalogItemListResponse>));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IQueryHandler<GetAvailableCatalogItemQuery, CatalogItemDto>));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IQueryHandler<ListAvailableCatalogItemsQuery, CatalogItemListResponse>));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ICommandValidator<CreateCatalogItemCommand>));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ICommandValidator<UpdateCatalogItemCommand>));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ICommandValidator<DiscontinueCatalogItemCommand>));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IQueryValidator<GetCatalogItemQuery>));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IQueryValidator<GetAvailableCatalogItemQuery>));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IQueryValidator<ListAvailableCatalogItemsQuery>));
        Assert.Equal(3, services.Count(descriptor => IsDomainEventHandler(descriptor.ServiceType)));
    }

    [Fact]
    public void Catalog_application_registration_rejects_null_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => DependencyInjection.AddCatalogApplication(null!));
    }

    private static bool IsDomainEventHandler(Type serviceType) =>
        serviceType.IsGenericType &&
        serviceType.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>);
}
