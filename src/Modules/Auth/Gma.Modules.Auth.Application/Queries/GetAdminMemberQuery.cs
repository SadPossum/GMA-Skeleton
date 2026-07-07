namespace Gma.Modules.Auth.Application.Queries;

using Gma.Modules.Auth.Contracts;
using Gma.Framework.Cqrs;

public sealed record GetAdminMemberQuery(Guid MemberId) : IQuery<AdminMemberDetails>;
