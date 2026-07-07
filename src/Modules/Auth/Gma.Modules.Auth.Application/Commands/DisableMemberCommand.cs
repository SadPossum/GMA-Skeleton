namespace Gma.Modules.Auth.Application.Commands;

using Gma.Framework.Cqrs;

public sealed record DisableMemberCommand(Guid MemberId, string Reason) : ITransactionalCommand<Unit>;
