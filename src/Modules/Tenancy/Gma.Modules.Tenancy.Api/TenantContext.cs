namespace Gma.Modules.Tenancy.Api;

using Gma.Framework.Naming;
using Microsoft.Extensions.Options;
using Gma.Framework.Tenancy;

internal sealed class TenantContext(IOptions<TenantOptions> options) : ITenantContextAccessor
{
    private string? tenantId;

    public bool IsEnabled => options.Value.Enabled;
    public string? TenantId => this.tenantId;

    public void SetTenant(string tenantId) => this.tenantId = TenantIds.Normalize(tenantId);

    public void ClearTenant() => this.tenantId = null;
}
