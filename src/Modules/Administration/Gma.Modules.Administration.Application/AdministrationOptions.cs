namespace Gma.Modules.Administration.Application;

public sealed class AdministrationOptions
{
    public const string SectionName = "Administration";

    public BootstrapOptions Bootstrap { get; set; } = new();

    public sealed class BootstrapOptions
    {
        public bool AllowWhenAssignmentsExist { get; set; }
        public string OwnerRoleName { get; set; } = "owner";
    }
}
