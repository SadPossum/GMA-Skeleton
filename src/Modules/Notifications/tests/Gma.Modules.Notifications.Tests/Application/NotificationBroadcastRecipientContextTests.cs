namespace Gma.Modules.Notifications.Tests;

using Gma.Modules.Notifications.Application;
using Gma.Modules.Notifications.Domain.Errors;
using Gma.Modules.Notifications.Domain.Entities;
using Gma.Modules.Notifications.Domain.ValueObjects;
using Xunit;
using ContractRecipientKind = Gma.Modules.Notifications.Contracts.NotificationBroadcastRecipientKind;

[Trait("Category", "Unit")]
public sealed class NotificationBroadcastRecipientContextTests
{
    [Fact]
    public void Create_normalizes_recipient_metadata()
    {
        Gma.Framework.Results.Result<NotificationBroadcastRecipientContext> result =
            NotificationBroadcastRecipientContext.Create(" tenant-a ", ContractRecipientKind.Admin, " admin-a ");

        Assert.True(result.IsSuccess);
        Assert.Equal("tenant-a", result.Value.TenantId);
        Assert.Equal(NotificationBroadcastRecipientKind.Admin, result.Value.RecipientKind);
        Assert.Equal("admin", result.Value.RecipientKindName);
        Assert.Equal("admin-a", result.Value.RecipientId);
        Assert.Equal("tenant:tenant-a", result.Value.RecipientScope);
    }

    [Fact]
    public void Create_uses_global_scope_when_tenant_is_absent()
    {
        Gma.Framework.Results.Result<NotificationBroadcastRecipientContext> result =
            NotificationBroadcastRecipientContext.Create(null, ContractRecipientKind.User, "user-a");

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.TenantId);
        Assert.Equal(NotificationBroadcastRead.GlobalRecipientScope, result.Value.RecipientScope);
    }

    [Fact]
    public void Create_rejects_invalid_tenant()
    {
        Gma.Framework.Results.Result<NotificationBroadcastRecipientContext> result =
            NotificationBroadcastRecipientContext.Create("tenant a", ContractRecipientKind.User, "user-a");

        Assert.True(result.IsFailure);
        Assert.Equal(NotificationsDomainErrors.TenantInvalid, result.Error);
    }

    [Fact]
    public void Create_rejects_invalid_recipient_kind()
    {
        Gma.Framework.Results.Result<NotificationBroadcastRecipientContext> result =
            NotificationBroadcastRecipientContext.Create("tenant-a", ContractRecipientKind.Unknown, "user-a");

        Assert.True(result.IsFailure);
        Assert.Equal(NotificationsDomainErrors.BroadcastRecipientKindInvalid, result.Error);
    }

    [Fact]
    public void Create_rejects_invalid_recipient_id()
    {
        Gma.Framework.Results.Result<NotificationBroadcastRecipientContext> result =
            NotificationBroadcastRecipientContext.Create("tenant-a", ContractRecipientKind.User, " ");

        Assert.True(result.IsFailure);
        Assert.Equal(NotificationsDomainErrors.UserIdInvalid, result.Error);
    }
}
