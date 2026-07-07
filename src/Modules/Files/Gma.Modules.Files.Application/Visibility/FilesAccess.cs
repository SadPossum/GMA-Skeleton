namespace Gma.Modules.Files.Application.Visibility;

using Gma.Framework.AccessControl;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Tenancy;

internal static class FilesAccess
{
    public static Result<Unit> EnsureUserSubject(
        AccessSubject? subject,
        ITenantContext tenantContext)
    {
        if (subject is null || subject.Kind != AccessSubjectKind.User)
        {
            return Result.Failure<Unit>(FilesApplicationErrors.AccessDenied);
        }

        if (!tenantContext.IsEnabled)
        {
            return Result.Success(Unit.Value);
        }

        if (string.IsNullOrWhiteSpace(tenantContext.TenantId))
        {
            return Result.Failure<Unit>(FilesApplicationErrors.TenantRequired);
        }

        return string.Equals(subject.TenantId, tenantContext.TenantId, StringComparison.Ordinal)
            ? Result.Success(Unit.Value)
            : Result.Failure<Unit>(FilesApplicationErrors.AccessDenied);
    }
}
