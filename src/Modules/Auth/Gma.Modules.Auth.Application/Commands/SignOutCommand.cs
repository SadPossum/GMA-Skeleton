namespace Gma.Modules.Auth.Application.Commands;

using Gma.Framework.Cqrs;

public sealed record SignOutCommand(Guid MemberId, string RefreshToken) : ITransactionalCommand<Unit>;
