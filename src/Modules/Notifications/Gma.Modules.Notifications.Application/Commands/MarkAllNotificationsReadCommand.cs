namespace Gma.Modules.Notifications.Application.Commands;

using Gma.Modules.Notifications.Contracts;
using Gma.Framework.AccessControl;
using Gma.Framework.Cqrs;

public sealed record MarkAllNotificationsReadCommand(AccessSubject Subject)
    : ITransactionalCommand<MarkAllNotificationsReadResponse>;
