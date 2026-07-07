namespace Catalog.Api;

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Gma.Framework.Security;

internal static class CatalogUserClaims
{
    public const string RegionCodeClaim = "catalog_region";

    public static string? GetCurrentUserId(HttpContext httpContext) =>
        httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
        httpContext.User.FindFirstValue(ApplicationClaimNames.Subject);

    public static string? GetCurrentRegionCode(HttpContext httpContext) =>
        httpContext.User.FindFirstValue(RegionCodeClaim);
}
