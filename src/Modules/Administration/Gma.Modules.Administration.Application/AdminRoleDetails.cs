namespace Gma.Modules.Administration.Application;

public sealed record AdminRoleDetails(
    Guid Id,
    string Name,
    IReadOnlyCollection<string> Permissions,
    int AssignmentCount);
