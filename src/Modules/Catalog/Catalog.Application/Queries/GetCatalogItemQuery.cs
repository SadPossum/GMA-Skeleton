namespace Catalog.Application.Queries;

using Catalog.Contracts;
using Gma.Framework.Cqrs;

public sealed record GetCatalogItemQuery(Guid ItemId) : IQuery<CatalogItemDto>;
