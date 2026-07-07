namespace Catalog.Application.Queries;

using Catalog.Contracts;
using Gma.Framework.AccessControl;
using Gma.Framework.Cqrs;

public sealed record GetAvailableCatalogItemQuery(
    Guid ItemId,
    AccessSubject Subject,
    string RegionCode,
    string? SubjectRegionCode)
    : IQuery<CatalogItemDto>;
