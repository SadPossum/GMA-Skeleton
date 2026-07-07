namespace Catalog.Application.Queries;

using Catalog.Contracts;
using Gma.Framework.AccessControl;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;

public sealed record ListAvailableCatalogItemsQuery(
    AccessSubject Subject,
    string RegionCode,
    string? SubjectRegionCode,
    int Page = PageRequest.DefaultPage,
    int PageSize = PageRequest.DefaultPageSize)
    : IQuery<CatalogItemListResponse>;
