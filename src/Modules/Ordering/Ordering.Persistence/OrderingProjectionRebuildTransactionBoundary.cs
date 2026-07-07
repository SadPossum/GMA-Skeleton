namespace Ordering.Persistence;

using Ordering.Contracts;
using Gma.Framework.ProjectionRebuild.EntityFrameworkCore;

internal sealed class OrderingProjectionRebuildTransactionBoundary(OrderingDbContext dbContext)
    : EfProjectionRebuildTransactionBoundary<OrderingDbContext>(dbContext, OrderingModuleMetadata.Name);
