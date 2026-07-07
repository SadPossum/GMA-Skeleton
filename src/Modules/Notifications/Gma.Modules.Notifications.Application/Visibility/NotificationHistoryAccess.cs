namespace Gma.Modules.Notifications.Application.Visibility;

using Gma.Framework.AccessControl;

internal static class NotificationHistoryAccess
{
    public static bool CanAccessUserHistory(AccessSubject subject, string? tenantId) =>
        subject.Kind == AccessSubjectKind.User &&
        (string.IsNullOrWhiteSpace(tenantId) ||
            string.Equals(subject.TenantId, tenantId, StringComparison.Ordinal));
}
