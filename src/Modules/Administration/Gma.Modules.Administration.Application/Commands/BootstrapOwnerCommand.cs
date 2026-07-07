namespace Gma.Modules.Administration.Application.Commands;

using Gma.Framework.Cqrs;

public sealed record BootstrapOwnerCommand(string ActorId, bool Confirmed) : ITransactionalCommand<Unit>;
