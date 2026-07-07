namespace Catalog.Domain.Visibility;

using Catalog.Domain.Errors;
using Catalog.Domain.ValueObjects;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed record CatalogViewer
{
    private CatalogViewer(CatalogUserId userId, string tenantId, CatalogRegionCode region)
    {
        this.UserId = userId;
        this.TenantId = tenantId;
        this.Region = region;
    }

    public CatalogUserId UserId { get; }
    public string TenantId { get; }
    public CatalogRegionCode Region { get; }

    public static Result<CatalogViewer> User(string? userId, string? tenantId, string? regionCode)
    {
        Result<CatalogUserId> userIdResult = CatalogUserId.Create(userId);
        if (userIdResult.IsFailure)
        {
            return Result.Failure<CatalogViewer>(CatalogDomainErrors.AccessDenied);
        }

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result.Failure<CatalogViewer>(CatalogDomainErrors.TenantRequired);
        }

        if (!TenantIds.TryNormalize(tenantId, out string? normalizedTenantId))
        {
            return Result.Failure<CatalogViewer>(CatalogDomainErrors.TenantInvalid);
        }

        Result<CatalogRegionCode> regionResult = CatalogRegionCode.Create(regionCode);
        return regionResult.IsFailure
            ? Result.Failure<CatalogViewer>(CatalogDomainErrors.AccessDenied)
            : Result.Success(new CatalogViewer(userIdResult.Value, normalizedTenantId, regionResult.Value));
    }
}
