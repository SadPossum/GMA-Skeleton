namespace Ordering.Tests;

using Catalog.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Ordering.Application;
using Ordering.Application.Commands;
using Ordering.Application.Tasks;
using Ordering.Application.Validation;
using Ordering.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Messaging;
using Gma.Framework.Scoping;
using Gma.Framework.Tasks;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrderingApplicationRegistrationTests
{
    [Fact]
    public void Ordering_application_registration_is_idempotent()
    {
        ServiceCollection services = new();

        services.AddOrderingApplication();
        services.AddOrderingApplication();
        services.AddOrderingTaskHandlers();
        services.AddOrderingTaskHandlers();

        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ICommandHandler<PlaceOrderCommand, OrderDto>));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ICommandValidator<PlaceOrderCommand>));
        Assert.Single(services, descriptor => IsProjectionHandler(descriptor, "CatalogItemCreatedProjectionHandler"));
        Assert.Single(services, descriptor => IsProjectionHandler(descriptor, "CatalogItemUpdatedProjectionHandler"));
        Assert.Single(services, descriptor => IsProjectionHandler(descriptor, "CatalogItemDiscontinuedProjectionHandler"));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(TaskHandlerRegistration));

        using ServiceProvider provider = services.BuildServiceProvider();
        IIntegrationEventSubscriptionRegistry registry =
            provider.GetRequiredService<IIntegrationEventSubscriptionRegistry>();
        ITaskHandlerRegistry taskRegistry = provider.GetRequiredService<ITaskHandlerRegistry>();

        Assert.Contains(registry.Subscriptions, subscription =>
            subscription.EventType == typeof(CatalogItemCreatedIntegrationEvent) &&
            subscription.Subject == CatalogIntegrationSubjects.ItemCreated &&
            subscription.HandlerName == OrderingModuleMetadata.CatalogItemCreatedProjectionHandlerName);
        Assert.Contains(registry.Subscriptions, subscription =>
            subscription.EventType == typeof(CatalogItemUpdatedIntegrationEvent) &&
            subscription.Subject == CatalogIntegrationSubjects.ItemUpdated &&
            subscription.HandlerName == OrderingModuleMetadata.CatalogItemUpdatedProjectionHandlerName);
        Assert.Contains(registry.Subscriptions, subscription =>
            subscription.EventType == typeof(CatalogItemDiscontinuedIntegrationEvent) &&
            subscription.Subject == CatalogIntegrationSubjects.ItemDiscontinued &&
            subscription.HandlerName == OrderingModuleMetadata.CatalogItemDiscontinuedProjectionHandlerName);
        TaskHandlerRegistration task = Assert.Single(taskRegistry.Registrations);
        Assert.Equal(OrderingModuleMetadata.Name, task.ModuleName);
        Assert.Equal(RebuildCatalogItemProjectionPayload.TaskName, task.TaskName);
        Assert.Equal(OrderingModuleMetadata.ProjectionWorkerGroup, task.WorkerGroup);
        Assert.Equal(typeof(RebuildCatalogItemProjectionPayload), task.PayloadType);
        Assert.Equal(ModuleTaskKind.OneShot, task.Kind);
        Assert.True(task.IsScopeAware());
        Assert.True(task.SupportsControlMessages);
        Assert.Equal(RebuildCatalogItemProjectionPayload.PayloadVersion, task.PayloadVersion);
    }

    [Fact]
    public void Ordering_application_registration_rejects_null_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => DependencyInjection.AddOrderingApplication(null!));
        Assert.Throws<ArgumentNullException>(() => DependencyInjection.AddOrderingTaskHandlers(null!));
    }

    [Fact]
    public void Ordering_application_registration_does_not_require_task_runtime_services()
    {
        ServiceCollection services = new();

        services.AddOrderingApplication();

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(TaskHandlerRegistration));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(ITaskHandlerRegistry));
    }

    private static bool IsProjectionHandler(ServiceDescriptor descriptor, string handlerTypeName) =>
        string.Equals(descriptor.ServiceType.Name, handlerTypeName, StringComparison.Ordinal) &&
        string.Equals(descriptor.ServiceType.Namespace, "Ordering.Application.Handlers", StringComparison.Ordinal);
}
