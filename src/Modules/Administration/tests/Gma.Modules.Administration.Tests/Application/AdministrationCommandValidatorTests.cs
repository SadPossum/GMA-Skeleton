namespace Gma.Modules.Administration.Tests;

using Gma.Modules.Administration.Application.Commands;
using Gma.Modules.Administration.Application.Validation;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AdministrationCommandValidatorTests
{
    [Fact]
    public void Bootstrap_validator_rejects_invalid_actor_but_leaves_confirmation_to_handler()
    {
        BootstrapOwnerCommandValidator validator = new();

        string[] invalidActorFailures = validator
            .Validate(new BootstrapOwnerCommand("bad actor", Confirmed: true))
            .ToArray();
        string[] unconfirmedFailures = validator
            .Validate(new BootstrapOwnerCommand("owner", Confirmed: false))
            .ToArray();

        Assert.Contains("Admin actor id is invalid.", invalidActorFailures);
        Assert.Empty(unconfirmedFailures);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Support Team")]
    public void Create_role_validator_rejects_missing_or_invalid_role_names(string roleName)
    {
        CreateRoleCommandValidator validator = new();

        string[] failures = validator
            .Validate(new CreateRoleCommand(roleName))
            .ToArray();

        Assert.NotEmpty(failures);
    }

    [Fact]
    public void Grant_permission_validator_rejects_invalid_permission_code()
    {
        GrantRolePermissionCommandValidator validator = new();

        string[] failures = validator
            .Validate(new GrantRolePermissionCommand("support", "auth"))
            .ToArray();

        Assert.Contains("Admin permission code is invalid.", failures);
    }

    [Fact]
    public void Assign_role_validator_rejects_invalid_actor_role_and_tenant()
    {
        AssignRoleCommandValidator validator = new();

        string[] failures = validator
            .Validate(new AssignRoleCommand("bad actor", "Support Team", "bad tenant"))
            .ToArray();

        Assert.Contains("Admin actor id is invalid.", failures);
        Assert.Contains("Admin role name is invalid.", failures);
        Assert.Contains("Tenant id is invalid.", failures);
    }
}
