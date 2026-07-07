namespace Ordering.Persistence;

using Gma.Framework.Domain;
using Gma.Framework.ProjectionRebuild;

public sealed class OrderingProjectionRebuildCheckpoint : ProjectionRebuildCheckpointState, ITenantScoped
{
    private OrderingProjectionRebuildCheckpoint() { }

    private OrderingProjectionRebuildCheckpoint(
        ProjectionRebuildCheckpointKey key,
        ProjectionRebuildCheckpoint checkpoint)
        : base(key, checkpoint, tenantScoped: true)
    {
    }

    public static OrderingProjectionRebuildCheckpoint Create(
        ProjectionRebuildCheckpointKey key,
        ProjectionRebuildCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(checkpoint);

        return new OrderingProjectionRebuildCheckpoint(key, checkpoint);
    }

    internal static OrderingProjectionRebuildCheckpoint CreateEmpty() => new();
}
