namespace Gma.Modules.Administration.Persistence.Entities;

using Gma.Framework.Administration;

public sealed class AdminPrincipal
{
    private readonly List<AdminPrincipalRole> roles = [];

    private AdminPrincipal() { }

    public AdminPrincipal(string id, DateTimeOffset createdAtUtc)
    {
        this.Id = AdminActor.System(id).Id;
        this.CreatedAtUtc = createdAtUtc;
    }

    public string Id { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public IReadOnlyCollection<AdminPrincipalRole> Roles => this.roles;
}
