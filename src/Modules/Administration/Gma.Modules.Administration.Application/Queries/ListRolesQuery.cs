namespace Gma.Modules.Administration.Application.Queries;

using Gma.Framework.Cqrs;

public sealed record ListRolesQuery : IQuery<IReadOnlyList<AdminRoleDetails>>;
