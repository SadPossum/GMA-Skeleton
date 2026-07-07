namespace Gma.Modules.Files.Application.Validation;

using Gma.Modules.Files.Application.Queries;
using Gma.Framework.AccessControl;
using Gma.Framework.Cqrs;

internal sealed class GetFileQueryValidator : IQueryValidator<GetFileQuery>
{
    public IEnumerable<string> Validate(GetFileQuery query)
    {
        if (query.FileId == Guid.Empty)
        {
            yield return "File id is required.";
        }

        if (query.Subject is null || query.Subject.Kind != AccessSubjectKind.User)
        {
            yield return "A user access subject is required.";
        }
    }
}
