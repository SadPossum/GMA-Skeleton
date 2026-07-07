namespace Catalog.Application.Validation;

using Catalog.Application.Queries;
using Catalog.Contracts;
using Gma.Framework.AccessControl;
using Gma.Framework.Cqrs;

internal sealed class ListAvailableCatalogItemsQueryValidator : IQueryValidator<ListAvailableCatalogItemsQuery>
{
    public IEnumerable<string> Validate(ListAvailableCatalogItemsQuery query)
    {
        if (query.Subject is null || query.Subject.Kind != AccessSubjectKind.User)
        {
            yield return "A user access subject is required.";
        }

        if (!CatalogRegionCodes.TryNormalize(query.RegionCode, out _))
        {
            yield return "Region code is invalid.";
        }
    }
}
