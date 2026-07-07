namespace Gma.Modules.Administration.Application.Commands;

using Gma.Framework.Cqrs;

public sealed record CreateRoleCommand(string Name) : ITransactionalCommand<AdminRoleDetails>;
