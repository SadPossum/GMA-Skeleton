namespace Gma.Modules.Tenancy.Contracts;

using Gma.Framework.ModuleComposition;
using Gma.Framework.Modules;

public static class TenancyModuleMetadata
{
    public const string Name = "tenancy";

    public static ModuleDescriptor Descriptor { get; } = ModuleDescriptor
        .Create(Name)
        .WithProfile(TenancyProfiles.Default)
        .Build();
}
