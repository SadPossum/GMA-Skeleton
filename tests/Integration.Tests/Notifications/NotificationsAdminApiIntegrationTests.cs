namespace Integration.Tests;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Integration.Tests.Support;
using Gma.Modules.Notifications.Admin.Contracts;
using Gma.Modules.Notifications.Contracts;
using Gma.Framework.Administration;
using Xunit;

public sealed class NotificationsAdminApiIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Admin_api_enforces_rbac_tenant_history_and_broadcast_inbox_scope()
    {
        await using NotificationsAdminApiTestApplication application =
            await NotificationsAdminApiTestApplication.CreateAsync();
        const string ownerActor = "owner-actor";
        const string strangerActor = "stranger-actor";
        await application.SeedOwnerAsync(ownerActor);
        Guid tenantNotificationId = Guid.Parse("11111111-aaaa-aaaa-aaaa-111111111111");
        await application.AddNotificationAsync(
                "tenant-a",
                "user-a",
                tenantNotificationId,
                "Tenant A notification",
                1);
        await application.AddNotificationAsync(
                "tenant-b",
                "user-a",
                Guid.Parse("22222222-bbbb-bbbb-bbbb-222222222222"),
                "Tenant B notification",
                2);
        using HttpClient ownerTenantA = CreateAuthenticatedClient(application, ownerActor, "tenant-a", "tenant-a");
        using HttpClient ownerTenantB = CreateAuthenticatedClient(application, ownerActor, "tenant-b", "tenant-b");
        using HttpClient platformOwner = CreateAuthenticatedClient(application, ownerActor, tokenScopeId: null, headerScopeId: null);
        using HttpClient strangerTenantA = CreateAuthenticatedClient(application, strangerActor, "tenant-a", "tenant-a");

        AdminNotificationHistoryListResponse history = await GetJsonAsync<AdminNotificationHistoryListResponse>(
                ownerTenantA,
                "/api/admin/notifications?userId=user-a&page=1&pageSize=10");
        using HttpResponseMessage deniedHistory = await strangerTenantA
            .GetAsync("/api/admin/notifications?userId=user-a");
        int deniedAuditCount = await application
            .CountAuditEntriesAsync(NotificationsAdminOperationNames.HistoryList, AdminErrors.Unauthorized.Code);

        AdminCreateNotificationBroadcastResponse tenantAdminBroadcast = await CreateBroadcastAsync(
                ownerTenantA,
                "/api/admin/notifications/broadcasts",
                NotificationBroadcastAudience.TenantAdmins,
                "tenant.admin.notice",
                "Tenant admin notice");
        AdminCreateNotificationBroadcastResponse tenantUserBroadcast = await CreateBroadcastAsync(
                ownerTenantA,
                "/api/admin/notifications/broadcasts",
                NotificationBroadcastAudience.TenantUsers,
                "tenant.user.notice",
                "Tenant user notice");
        AdminCreateNotificationBroadcastResponse platformAdminBroadcast = await CreateBroadcastAsync(
                platformOwner,
                "/api/admin/notifications/platform-broadcasts",
                NotificationBroadcastAudience.PlatformAdmins,
                "platform.admin.notice",
                "Platform admin notice");

        AdminNotificationBroadcastListResponse tenantBroadcasts =
            await GetJsonAsync<AdminNotificationBroadcastListResponse>(
                    ownerTenantA,
                    "/api/admin/notifications/broadcasts?page=1&pageSize=10");
        AdminNotificationBroadcastListResponse platformBroadcasts =
            await GetJsonAsync<AdminNotificationBroadcastListResponse>(
                    platformOwner,
                    "/api/admin/notifications/platform-broadcasts?page=1&pageSize=10");
        NotificationBroadcastListResponse tenantAInbox =
            await GetJsonAsync<NotificationBroadcastListResponse>(
                    ownerTenantA,
                    "/api/admin/notifications/broadcasts/inbox?page=1&pageSize=10");
        using HttpResponseMessage markTenantAdminRead = await ownerTenantA
            .PostAsync(
                $"/api/admin/notifications/broadcasts/inbox/{tenantAdminBroadcast.BroadcastId}/read",
                content: null);
        NotificationBroadcastListResponse tenantAUnread =
            await GetJsonAsync<NotificationBroadcastListResponse>(
                    ownerTenantA,
                    "/api/admin/notifications/broadcasts/inbox?unreadOnly=true&page=1&pageSize=10");
        MarkAllNotificationBroadcastsReadResponse tenantAMarkAll =
            await PostJsonAsync<MarkAllNotificationBroadcastsReadResponse>(
                    ownerTenantA,
                    "/api/admin/notifications/broadcasts/inbox/read-all",
                    new { });
        NotificationBroadcastListResponse tenantBInbox =
            await GetJsonAsync<NotificationBroadcastListResponse>(
                    ownerTenantB,
                    "/api/admin/notifications/broadcasts/inbox?page=1&pageSize=10");

        Assert.Equal(1, history.TotalCount);
        AdminNotificationHistoryItem historyItem = Assert.Single(history.Items);
        Assert.Equal(tenantNotificationId, historyItem.NotificationId);
        Assert.Equal("tenant-a", historyItem.ScopeId);
        Assert.Equal(HttpStatusCode.Forbidden, deniedHistory.StatusCode);
        Assert.Equal(1, deniedAuditCount);

        Assert.Equal(2, tenantBroadcasts.TotalCount);
        Assert.Contains(tenantBroadcasts.Items, item => item.BroadcastId == tenantAdminBroadcast.BroadcastId);
        Assert.Contains(tenantBroadcasts.Items, item => item.BroadcastId == tenantUserBroadcast.BroadcastId);
        AdminNotificationBroadcastItem platformBroadcast = Assert.Single(platformBroadcasts.Items);
        Assert.Equal(platformAdminBroadcast.BroadcastId, platformBroadcast.BroadcastId);
        Assert.Null(platformBroadcast.ScopeId);

        Assert.Equal(2, tenantAInbox.TotalCount);
        Assert.Equal(2, tenantAInbox.UnreadCount);
        Assert.Contains(tenantAInbox.Items, item => item.BroadcastId == tenantAdminBroadcast.BroadcastId);
        Assert.Contains(tenantAInbox.Items, item => item.BroadcastId == platformAdminBroadcast.BroadcastId);
        Assert.DoesNotContain(tenantAInbox.Items, item => item.BroadcastId == tenantUserBroadcast.BroadcastId);
        Assert.Equal(HttpStatusCode.NoContent, markTenantAdminRead.StatusCode);
        NotificationBroadcastItem unread = Assert.Single(tenantAUnread.Items);
        Assert.Equal(platformAdminBroadcast.BroadcastId, unread.BroadcastId);
        Assert.Equal(1, tenantAMarkAll.UpdatedCount);
        NotificationBroadcastItem tenantBPlatform = Assert.Single(tenantBInbox.Items);
        Assert.Equal(platformAdminBroadcast.BroadcastId, tenantBPlatform.BroadcastId);
        Assert.Null(tenantBPlatform.ReadAtUtc);
        Assert.Equal(1, tenantBInbox.UnreadCount);
    }

    private static HttpClient CreateAuthenticatedClient(
        NotificationsAdminApiTestApplication application,
        string actorId,
        string? tokenScopeId,
        string? headerScopeId)
    {
        HttpClient client = application.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            NotificationsAdminApiTestApplication.CreateAccessToken(actorId, tokenScopeId));

        if (!string.IsNullOrWhiteSpace(headerScopeId))
        {
            client.DefaultRequestHeaders.Add("X-Tenant-Id", headerScopeId);
        }

        return client;
    }

    private static Task<AdminCreateNotificationBroadcastResponse> CreateBroadcastAsync(
        HttpClient client,
        string requestUri,
        NotificationBroadcastAudience audience,
        string name,
        string title) =>
        PostJsonAsync<AdminCreateNotificationBroadcastResponse>(
            client,
            requestUri,
            new
            {
                audience,
                name,
                version = 1,
                title,
                body = (string?)null,
                severity = NotificationSeverity.Info,
                occurredAtUtc = new DateTimeOffset(2026, 7, 5, 12, 0, 0, TimeSpan.Zero),
                payload = new { title }
            });

    private static async Task<T> GetJsonAsync<T>(HttpClient client, string requestUri)
    {
        using HttpResponseMessage response = await client.GetAsync(requestUri);
        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode,
            $"Status={(int)response.StatusCode} {response.StatusCode}{Environment.NewLine}{body}");
        T? value = await response.Content.ReadFromJsonAsync<T>();
        Assert.NotNull(value);
        return value;
    }

    private static async Task<T> PostJsonAsync<T>(HttpClient client, string requestUri, object value)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(requestUri, value);
        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode,
            $"Status={(int)response.StatusCode} {response.StatusCode}{Environment.NewLine}{body}");
        T? result = await response.Content.ReadFromJsonAsync<T>();
        Assert.NotNull(result);
        return result;
    }
}
