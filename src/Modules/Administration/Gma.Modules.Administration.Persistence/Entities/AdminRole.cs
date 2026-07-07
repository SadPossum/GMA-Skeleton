namespace Gma.Modules.Administration.Persistence.Entities;

using Gma.Modules.Administration.Application;

public sealed class AdminRole
{
    private readonly List<AdminRolePermission> permissions = [];
    private readonly List<AdminPrincipalRole> assignments = [];

    private AdminRole() { }

    public AdminRole(Guid id, string name, DateTimeOffset createdAtUtc)
    {
        this.Id = id;
        this.Name = NormalizeName(name);
        this.CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public IReadOnlyCollection<AdminRolePermission> Permissions => this.permissions;
    public IReadOnlyCollection<AdminPrincipalRole> Assignments => this.assignments;

    public static string NormalizeName(string name) => AdminRoleName.Normalize(name);
}
