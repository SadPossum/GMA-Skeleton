namespace Catalog.Domain.Visibility;

using Catalog.Domain.Errors;
using Catalog.Domain.ValueObjects;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed record CatalogViewer
{
    private CatalogViewer(CatalogUserId userId, string scopeId, CatalogRegionCode region)
    {
        this.UserId = userId;
        this.ScopeId = scopeId;
        this.Region = region;
    }

    public CatalogUserId UserId { get; }
    public string ScopeId { get; }
    public CatalogRegionCode Region { get; }

    public static Result<CatalogViewer> User(string? userId, string? scopeId, string? regionCode)
    {
        Result<CatalogUserId> userIdResult = CatalogUserId.Create(userId);
        if (userIdResult.IsFailure)
        {
            return Result.Failure<CatalogViewer>(CatalogDomainErrors.AccessDenied);
        }

        if (string.IsNullOrWhiteSpace(scopeId))
        {
            return Result.Failure<CatalogViewer>(CatalogDomainErrors.TenantRequired);
        }

        if (!ScopeIds.TryNormalize(scopeId, out string? normalizedScopeId))
        {
            return Result.Failure<CatalogViewer>(CatalogDomainErrors.TenantInvalid);
        }

        Result<CatalogRegionCode> regionResult = CatalogRegionCode.Create(regionCode);
        return regionResult.IsFailure
            ? Result.Failure<CatalogViewer>(CatalogDomainErrors.AccessDenied)
            : Result.Success(new CatalogViewer(userIdResult.Value, normalizedScopeId, regionResult.Value));
    }
}
