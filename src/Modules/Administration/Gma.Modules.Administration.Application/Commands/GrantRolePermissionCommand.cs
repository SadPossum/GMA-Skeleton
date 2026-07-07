namespace Gma.Modules.Administration.Application.Commands;

using Gma.Framework.Cqrs;

public sealed record GrantRolePermissionCommand(string RoleName, string PermissionCode) : ITransactionalCommand<Unit>;
