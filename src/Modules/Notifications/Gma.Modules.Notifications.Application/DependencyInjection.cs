namespace Gma.Modules.Notifications.Application;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Gma.Modules.Notifications.Application.Handlers;
using Gma.Modules.Notifications.Contracts;
using Gma.Framework.Application.Composition;
using Gma.Framework.Messaging;

public static class DependencyInjection
{
    public const string UserNotificationRequestHandlerNameSuffix = "notification-request";

    public static IServiceCollection AddNotificationsApplication(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (!services.Any(descriptor => descriptor.ServiceType == typeof(NotificationStreamOptionsRegistrationMarker)))
        {
            if (configuration is not null)
            {
                NotificationStreamOptionsValidation.GetValidatedOptions(configuration);
            }

            services.AddSingleton<NotificationStreamOptionsRegistrationMarker>();
            OptionsBuilder<NotificationStreamOptions> optionsBuilder = services.AddOptions<NotificationStreamOptions>();
            if (configuration is not null)
            {
                optionsBuilder.Bind(configuration.GetSection(NotificationStreamOptions.SectionName));
            }

            optionsBuilder.ValidateOnStart();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IValidateOptions<NotificationStreamOptions>, NotificationStreamOptionsValidator>());
        }

        services.AddApplicationServicesFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }

    public static IServiceCollection AddUserNotificationRequestSubscription(
        this IServiceCollection services,
        string producerModule,
        string? handlerName = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        string normalizedProducerModule = IntegrationEventNaming.NormalizeModuleName(
            producerModule,
            nameof(producerModule));
        string normalizedHandlerName = IntegrationEventNaming.NormalizeHandlerName(
            handlerName ?? $"{normalizedProducerModule}-{UserNotificationRequestHandlerNameSuffix}",
            nameof(handlerName));

        services.AddIntegrationEventHandler<UserNotificationRequestedIntegrationEvent, UserNotificationRequestedIntegrationEventHandler>(
            NotificationsModuleMetadata.Name,
            normalizedProducerModule,
            UserNotificationRequestedIntegrationEvent.EventType,
            UserNotificationRequestedIntegrationEvent.EventVersion,
            normalizedHandlerName);

        return services;
    }

    private sealed class NotificationStreamOptionsRegistrationMarker;
}
