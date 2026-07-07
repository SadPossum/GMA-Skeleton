namespace Catalog.Application.Validation;

using Catalog.Application.Queries;
using Catalog.Contracts;
using Gma.Framework.AccessControl;
using Gma.Framework.Cqrs;

internal sealed class GetAvailableCatalogItemQueryValidator : IQueryValidator<GetAvailableCatalogItemQuery>
{
    public IEnumerable<string> Validate(GetAvailableCatalogItemQuery query)
    {
        if (query.ItemId == Guid.Empty)
        {
            yield return "Catalog item id is required.";
        }

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
