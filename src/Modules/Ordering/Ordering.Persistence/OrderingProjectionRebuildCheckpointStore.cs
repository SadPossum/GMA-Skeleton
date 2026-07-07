namespace Ordering.Persistence;

using Ordering.Contracts;
using Gma.Framework.ProjectionRebuild;
using Gma.Framework.ProjectionRebuild.EntityFrameworkCore;

internal sealed class OrderingProjectionRebuildCheckpointStore(OrderingDbContext dbContext)
    : EfProjectionRebuildCheckpointStore<OrderingDbContext, OrderingProjectionRebuildCheckpoint>(
        dbContext,
        OrderingModuleMetadata.Name,
        tenantScoped: true,
        OrderingProjectionRebuildCheckpoint.CreateEmpty);
