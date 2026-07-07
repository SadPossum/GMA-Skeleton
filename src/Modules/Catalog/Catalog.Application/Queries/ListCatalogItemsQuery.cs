namespace Catalog.Application.Queries;

using Catalog.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;

public sealed record ListCatalogItemsQuery(
    int Page = PageRequest.DefaultPage,
    int PageSize = PageRequest.DefaultPageSize)
    : IQuery<CatalogItemListResponse>;
