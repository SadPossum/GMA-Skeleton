namespace Catalog.Application.Commands;

using Gma.Framework.Cqrs;

public sealed record DiscontinueCatalogItemCommand(Guid ItemId) : ITransactionalCommand<Unit>;
