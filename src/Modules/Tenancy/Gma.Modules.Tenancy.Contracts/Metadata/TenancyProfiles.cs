namespace Gma.Modules.Tenancy.Contracts;

using Gma.Framework.ModuleComposition;
using Gma.Framework.Tenancy;

public static class TenancyProfiles
{
    public const string DefaultName = "default";

    public static ModuleProfileDescriptor Default { get; } = new(
        TenancyModuleMetadata.Name,
        DefaultName,
        provides:
        [
            TenancyCompositionFeatures.ContextProvided("tenancy/default"),
            TenancyCompositionFeatures.HeaderResolutionProvided("tenancy/default")
        ],
        displayName: "Tenancy default",
        description: "Resolves tenant context from configured HTTP tenant headers.");
}
