namespace Catalog.Domain.Visibility;

using Catalog.Domain.Errors;
using Catalog.Domain.ValueObjects;
using Gma.Framework.Results;

public static class CatalogAvailabilityPolicy
{
    public static Result<AvailableCatalogItemsScope> CanViewAvailableItems(
        CatalogViewer viewer,
        string requestedRegionCode)
    {
        Result<CatalogRegionCode> requestedRegionResult = CatalogRegionCode.Create(requestedRegionCode);
        if (requestedRegionResult.IsFailure)
        {
            return Result.Failure<AvailableCatalogItemsScope>(requestedRegionResult.Error);
        }

        CatalogRegionCode requestedRegion = requestedRegionResult.Value;
        return viewer.Region == requestedRegion
            ? Result.Success(new AvailableCatalogItemsScope(viewer.ScopeId, requestedRegion))
            : Result.Failure<AvailableCatalogItemsScope>(CatalogDomainErrors.AccessDenied);
    }
}
