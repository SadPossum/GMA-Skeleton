namespace Gma.Modules.Auth.Application.Commands;

using Gma.Framework.Cqrs;

public sealed record ResetMemberPasswordCommand(Guid MemberId, string NewPassword) : ITransactionalCommand<Unit>;
