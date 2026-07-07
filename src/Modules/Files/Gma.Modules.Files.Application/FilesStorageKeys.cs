namespace Gma.Modules.Files.Application;

using System.Security.Cryptography;
using System.Text;
using Gma.Framework.AccessControl;
using Gma.Framework.FileManagement;
using Gma.Framework.Tenancy;

internal static class FilesStorageKeys
{
    public static FileStorageObjectKey For(
        Guid fileId,
        AccessSubject subject,
        ITenantContext tenantContext)
    {
        if (fileId == Guid.Empty)
        {
            throw new ArgumentException("File id cannot be empty.", nameof(fileId));
        }

        string tenantSegment = TenantSegment(tenantContext);
        string subjectSegment = SubjectSegment(subject);
        return new FileStorageObjectKey($"files/{tenantSegment}/{subjectSegment}/{fileId:N}");
    }

    private static string TenantSegment(ITenantContext tenantContext)
    {
        if (!tenantContext.IsEnabled)
        {
            return "global";
        }

        if (string.IsNullOrWhiteSpace(tenantContext.TenantId))
        {
            throw new InvalidOperationException("Tenant id is required when tenancy is enabled.");
        }

        return $"tenant-{HashSegment(tenantContext.TenantId)}";
    }

    private static string SubjectSegment(AccessSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        string kind = subject.Kind switch
        {
            AccessSubjectKind.User => "user",
            AccessSubjectKind.AdminActor => "admin",
            AccessSubjectKind.Service => "service",
            AccessSubjectKind.System => "system",
            _ => throw new ArgumentException("File subject kind is not supported.", nameof(subject))
        };

        return $"{kind}-{HashSegment(subject.Id)}";
    }

    private static string HashSegment(string value)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant()[..16];
    }
}
