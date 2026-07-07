namespace Gma.Modules.Auth.Application.Commands;

using Gma.Framework.Cqrs;

public sealed record EnableMemberCommand(Guid MemberId) : ITransactionalCommand<Unit>;
