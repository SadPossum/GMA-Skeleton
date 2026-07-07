namespace Catalog.Application.Commands;

using Catalog.Contracts;
using Gma.Framework.Cqrs;

public sealed record CreateCatalogItemCommand(
    string Sku,
    string Name,
    decimal Price,
    string Currency,
    IReadOnlyCollection<string>? AvailableRegions = null)
    : ITransactionalCommand<CatalogItemDto>;
