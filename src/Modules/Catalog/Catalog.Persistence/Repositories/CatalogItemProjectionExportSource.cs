namespace Catalog.Persistence.Repositories;

using Catalog.Contracts;
using Catalog.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Gma.Framework.ProjectionRebuild;

internal sealed class CatalogItemProjectionExportSource(CatalogDbContext dbContext) : ICatalogItemProjectionExportSource
{
    public async Task<ProjectionReadBatch<CatalogItemProjectionExport>> ReadAsync(
        ProjectionRebuildRequest request,
        string? cursor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        string? normalizedCursor = NormalizeCursor(cursor);
        IQueryable<CatalogItem> query = dbContext.CatalogItems
            .AsNoTracking()
            .Include(item => item.AvailableRegions);
        if (normalizedCursor is not null)
        {
            query = this.ApplyCursor(query, normalizedCursor);
        }

        List<CatalogItem> rows = await query
            .OrderBy(item => item.Sku)
            .Take(request.BatchSize + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        bool hasMore = rows.Count > request.BatchSize;
        CatalogItem[] page = rows.Take(request.BatchSize).ToArray();
        string? nextCursor = page.Length == 0 ? null : page[^1].Sku.Value;

        return new ProjectionReadBatch<CatalogItemProjectionExport>(
            page.Select(Map).ToArray(),
            nextCursor,
            hasMore);
    }

    private static CatalogItemProjectionExport Map(CatalogItem item) =>
        new(
            item.ScopeId,
            item.Id,
            item.Sku.Value,
            item.Name.Value,
            item.Price.Value,
            item.Currency.Value,
            MapStatus(item.Status),
            item.AvailableRegions
                .Select(region => region.Region.Value)
                .Order(StringComparer.Ordinal)
                .ToArray());

    private static CatalogItemStatus MapStatus(CatalogItemState status) =>
        status switch
        {
            CatalogItemState.Active => CatalogItemStatus.Active,
            CatalogItemState.Discontinued => CatalogItemStatus.Discontinued,
            _ => CatalogItemStatus.Unknown
        };

    private IQueryable<CatalogItem> ApplyCursor(IQueryable<CatalogItem> query, string normalizedCursor)
    {
        if (dbContext.Database.IsSqlServer())
        {
            return dbContext.CatalogItems
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM [catalog].[items]
                    WHERE [Sku] > {normalizedCursor}
                    """)
                .AsNoTracking()
                .Include(item => item.AvailableRegions);
        }

        if (dbContext.Database.IsNpgsql())
        {
            return dbContext.CatalogItems
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM "catalog"."items"
                    WHERE "Sku" > {normalizedCursor}
                    """)
                .AsNoTracking()
                .Include(item => item.AvailableRegions);
        }

#pragma warning disable CA1309
        return query.Where(item => string.Compare(item.Sku.Value, normalizedCursor) > 0);
#pragma warning restore CA1309
    }

    private static string? NormalizeCursor(string? cursor) =>
        string.IsNullOrWhiteSpace(cursor)
            ? null
            : CatalogItem.NormalizeSku(cursor);
}
