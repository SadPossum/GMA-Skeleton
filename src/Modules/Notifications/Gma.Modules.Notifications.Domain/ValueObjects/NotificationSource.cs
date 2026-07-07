namespace Gma.Modules.Notifications.Domain.ValueObjects;

using Gma.Modules.Notifications.Domain.Aggregates;
using Gma.Modules.Notifications.Domain.Errors;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed record NotificationSource
{
    private NotificationSource()
    {
    }

    private NotificationSource(string module, string name, int version)
    {
        this.Module = module;
        this.Name = name;
        this.Version = version;
    }

    public string Module { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public int Version { get; private set; }

    public static Result<NotificationSource> Create(string? module, string? name, int version)
    {
        if (string.IsNullOrWhiteSpace(module))
        {
            return Result.Failure<NotificationSource>(NotificationsDomainErrors.ModuleInvalid);
        }

        string normalizedModule = module.Trim().ToLowerInvariant();
        if (normalizedModule.Length > UserNotification.ModuleMaxLength ||
            !SharedNameSegments.IsKebabSegment(normalizedModule))
        {
            return Result.Failure<NotificationSource>(NotificationsDomainErrors.ModuleInvalid);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<NotificationSource>(NotificationsDomainErrors.NameInvalid);
        }

        string normalizedName = name.Trim().ToLowerInvariant();
        string[] segments = normalizedName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (normalizedName.Length > UserNotification.NameMaxLength ||
            segments.Length != normalizedName.Count(character => character == '.') + 1 ||
            !segments.All(SharedNameSegments.IsKebabSegment))
        {
            return Result.Failure<NotificationSource>(NotificationsDomainErrors.NameInvalid);
        }

        return version > 0
            ? Result.Success(new NotificationSource(normalizedModule, normalizedName, version))
            : Result.Failure<NotificationSource>(NotificationsDomainErrors.VersionInvalid);
    }
}
