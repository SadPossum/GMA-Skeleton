namespace Gma.Modules.TaskRuntime.Admin.Contracts;

using Gma.Framework.Administration;
using Gma.Modules.TaskRuntime.Contracts;

public static class TaskRuntimeAdminPermissions
{
    public static readonly AdminPermission RunsRead = AdminPermission.Create(TaskRuntimePermissionCodes.RunsRead);
    public static readonly AdminPermission RunsCreate = AdminPermission.Create(TaskRuntimePermissionCodes.RunsCreate);
    public static readonly AdminPermission RunsCancel = AdminPermission.Create(TaskRuntimePermissionCodes.RunsCancel);
    public static readonly AdminPermission RunsRetry = AdminPermission.Create(TaskRuntimePermissionCodes.RunsRetry);
    public static readonly AdminPermission RunsControl = AdminPermission.Create(TaskRuntimePermissionCodes.RunsControl);
}
