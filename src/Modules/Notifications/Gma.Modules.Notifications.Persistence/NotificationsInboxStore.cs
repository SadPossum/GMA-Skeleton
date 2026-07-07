namespace Gma.Modules.Notifications.Persistence;

using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

internal sealed class NotificationsInboxStore(
    NotificationsDbContext dbContext,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : EfInboxStore<NotificationsDbContext>(dbContext, clock, idGenerator, NotificationsMigrations.Schema);
