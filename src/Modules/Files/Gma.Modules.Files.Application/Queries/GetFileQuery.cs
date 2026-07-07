namespace Gma.Modules.Files.Application.Queries;

using Gma.Modules.Files.Application.ReadModels;
using Gma.Framework.AccessControl;
using Gma.Framework.Cqrs;

public sealed record GetFileQuery(Guid FileId, AccessSubject Subject) : IQuery<FileDownload>;
