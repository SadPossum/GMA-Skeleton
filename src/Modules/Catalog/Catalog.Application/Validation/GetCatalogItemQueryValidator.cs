namespace Catalog.Application.Validation;

using Catalog.Application.Queries;
using Gma.Framework.Cqrs;

internal sealed class GetCatalogItemQueryValidator : IQueryValidator<GetCatalogItemQuery>
{
    public IEnumerable<string> Validate(GetCatalogItemQuery query)
    {
        if (query.ItemId == Guid.Empty)
        {
            yield return "Catalog item id is required.";
        }
    }
}
