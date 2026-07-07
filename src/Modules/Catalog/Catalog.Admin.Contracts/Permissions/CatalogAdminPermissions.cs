namespace Catalog.Admin.Contracts;

using Catalog.Contracts;
using Gma.Framework.Administration;

public static class CatalogAdminPermissions
{
    public static readonly AdminPermission ItemsRead = AdminPermission.Create(CatalogAdminPermissionCodes.ItemsRead);
    public static readonly AdminPermission ItemsCreate = AdminPermission.Create(CatalogAdminPermissionCodes.ItemsCreate);
    public static readonly AdminPermission ItemsUpdate = AdminPermission.Create(CatalogAdminPermissionCodes.ItemsUpdate);
    public static readonly AdminPermission ItemsDiscontinue = AdminPermission.Create(CatalogAdminPermissionCodes.ItemsDiscontinue);
}
