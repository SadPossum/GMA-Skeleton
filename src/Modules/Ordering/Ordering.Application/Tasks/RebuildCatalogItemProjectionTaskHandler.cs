namespace Ordering.Application.Tasks;

using Catalog.Contracts;
using Ordering.Contracts;
using Gma.Framework.ProjectionRebuild;
using Gma.Framework.ProjectionRebuild.Tasks;
using Gma.Framework.Tasks;

internal sealed class RebuildCatalogItemProjectionTaskHandler(
    ICatalogItemProjectionExportSource source,
    IProjectionRebuildWriter<CatalogItemProjectionExport> writer,
    TaskProjectionRebuildRunner<CatalogItemProjectionExport> runner)
    : ITaskHandler<RebuildCatalogItemProjectionPayload>
{
    public async Task HandleAsync(
        RebuildCatalogItemProjectionPayload payload,
        TaskExecutionContext context,
        CancellationToken cancellationToken)
    {
        ProjectionRebuildRequest request = new(
            OrderingModuleMetadata.CatalogItemProjectionName,
            payload.ProjectionVersion,
            payload.BatchSize,
            payload.DryRun,
            payload.Cursor);

        await runner
            .RunAsync(
                OrderingModuleMetadata.Name,
                request,
                source,
                writer,
                context,
                tenantScoped: true,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
