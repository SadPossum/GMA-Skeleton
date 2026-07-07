namespace Gma.Modules.Administration.Application.Commands;

using Gma.Framework.Cqrs;

public sealed record AssignRoleCommand(string ActorId, string RoleName, string? TenantId) : ITransactionalCommand<Unit>;
