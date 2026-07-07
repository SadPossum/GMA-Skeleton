namespace Gma.Modules.Administration.Application;

using Gma.Framework.Results;

public static class AdministrationApplicationErrors
{
    public static readonly Error RoleAlreadyExists = new("Administration.RoleAlreadyExists", "An admin role with this name already exists.");
    public static readonly Error RoleNotFound = new("Administration.RoleNotFound", "The requested admin role was not found.");
    public static readonly Error RoleNameInvalid = new("Administration.RoleNameInvalid", "The admin role name is not valid.");
    public static readonly Error PermissionCodeInvalid = new("Administration.PermissionCodeInvalid", "The admin permission code is not valid.");
    public static readonly Error PermissionAlreadyGranted = new("Administration.PermissionAlreadyGranted", "The role already has this permission.");
    public static readonly Error AssignmentAlreadyExists = new("Administration.AssignmentAlreadyExists", "The admin principal already has this role assignment.");
    public static readonly Error ActorRequired = new("Administration.ActorRequired", "An admin actor id is required.");
    public static readonly Error ActorInvalid = new("Administration.ActorInvalid", "The admin actor id is not valid.");
    public static readonly Error RoleNameRequired = new("Administration.RoleNameRequired", "An admin role name is required.");
    public static readonly Error TenantInvalid = new("Administration.TenantInvalid", "The tenant id is not valid.");
}
