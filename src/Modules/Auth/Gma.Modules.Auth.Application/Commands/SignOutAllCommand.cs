namespace Gma.Modules.Auth.Application.Commands;

using Gma.Framework.Cqrs;

public sealed record SignOutAllCommand(Guid MemberId) : ITransactionalCommand<Unit>;
