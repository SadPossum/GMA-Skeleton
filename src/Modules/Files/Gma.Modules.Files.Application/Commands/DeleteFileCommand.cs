namespace Gma.Modules.Files.Application.Commands;

using Gma.Framework.AccessControl;
using Gma.Framework.Cqrs;

public sealed record DeleteFileCommand(Guid FileId, AccessSubject Subject) : ICommand<Unit>;
