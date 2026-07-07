namespace Ordering.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Gma.Framework.ProjectionRebuild.EntityFrameworkCore;

internal sealed class OrderingProjectionRebuildCheckpointConfiguration
    : IEntityTypeConfiguration<OrderingProjectionRebuildCheckpoint>
{
    public void Configure(EntityTypeBuilder<OrderingProjectionRebuildCheckpoint> builder)
        => builder.ConfigureProjectionRebuildCheckpointState();
}
