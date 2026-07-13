namespace Integration.Tests;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Gma.Modules.Notifications.Contracts;
using Integration.Tests.Support;
using Xunit;
using DomainBroadcastAudience = Gma.Modules.Notifications.Domain.ValueObjects.NotificationBroadcastAudience;
using DomainTagKind = Gma.Modules.Notifications.Domain.ValueObjects.NotificationTagKind;

public sealed class NotificationsApiIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task User_preferences_are_tenant_and_user_scoped_and_default_to_enabled()
    {
        await using NotificationsApiTestApplication application = await NotificationsApiTestApplication
            .CreateAsync();
        await application.AddTagDefinitionAsync("tenant-a", "domain:security", DomainTagKind.Domain);
        using HttpClient userA = CreateAuthenticatedClient(application, "tenant-a", "user-a");
        using HttpClient userB = CreateAuthenticatedClient(application, "tenant-a", "user-b");

        NotificationPreferenceListResponse initial = await GetJsonAsync<NotificationPreferenceListResponse>(
            userA,
            "/api/notifications/preferences");
        using HttpResponseMessage updateResponse = await userA.PutAsJsonAsync(
            "/api/notifications/preferences/domain:security",
            new SetNotificationPreferenceRequest(Enabled: false));
        updateResponse.EnsureSuccessStatusCode();
        NotificationPreferenceItem? updated = await updateResponse.Content
            .ReadFromJsonAsync<NotificationPreferenceItem>();
        NotificationPreferenceListResponse userAAfter = await GetJsonAsync<NotificationPreferenceListResponse>(
            userA,
            "/api/notifications/preferences");
        NotificationPreferenceListResponse userBList = await GetJsonAsync<NotificationPreferenceListResponse>(
            userB,
            "/api/notifications/preferences");

        Assert.True(Assert.Single(initial.Items).Enabled);
        Assert.NotNull(updated);
        Assert.False(updated.Enabled);
        Assert.False(Assert.Single(userAAfter.Items).Enabled);
        Assert.True(Assert.Single(userBList.Items).Enabled);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task User_history_api_enforces_tenant_and_user_scope_and_marks_read()
    {
        await using NotificationsApiTestApplication application = await NotificationsApiTestApplication
            .CreateAsync();
        Guid firstId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Guid secondId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        Guid otherUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        await application.AddNotificationAsync("tenant-a", "user-a", firstId, "First", 1);
        await application.AddNotificationAsync("tenant-a", "user-a", secondId, "Second", 2);
        await application.AddNotificationAsync("tenant-a", "user-b", otherUserId, "Other user", 3);
        await application.AddNotificationAsync(
                "tenant-b",
                "user-a",
                Guid.Parse("44444444-4444-4444-4444-444444444444"),
                "Other tenant",
                4);
        using HttpClient client = CreateAuthenticatedClient(application, "tenant-a", "user-a");

        NotificationHistoryListResponse list = await GetJsonAsync<NotificationHistoryListResponse>(
                client,
                "/api/notifications/?page=1&pageSize=10");
        using HttpResponseMessage otherUserResponse = await client
            .GetAsync($"/api/notifications/{otherUserId}");
        using HttpResponseMessage markRead = await client
            .PostAsync($"/api/notifications/{secondId}/read", content: null);
        NotificationHistoryListResponse unread = await GetJsonAsync<NotificationHistoryListResponse>(
                client,
                "/api/notifications/?unreadOnly=true&page=1&pageSize=10");
        MarkAllNotificationsReadResponse markAll = await PostJsonAsync<MarkAllNotificationsReadResponse>(
                client,
                "/api/notifications/read-all",
                value: new { });
        NotificationHistoryListResponse afterMarkAll = await GetJsonAsync<NotificationHistoryListResponse>(
                client,
                "/api/notifications/?page=1&pageSize=10");
        using HttpClient tenantMismatchClient = CreateAuthenticatedClient(application, "tenant-b", "user-a");
        tenantMismatchClient.DefaultRequestHeaders.Remove("X-Tenant-Id");
        tenantMismatchClient.DefaultRequestHeaders.Add("X-Tenant-Id", "tenant-a");
        using HttpResponseMessage tenantMismatch = await tenantMismatchClient
            .GetAsync("/api/notifications/");

        Assert.Equal(2, list.TotalCount);
        Assert.Equal(2, list.UnreadCount);
        Assert.Equal([secondId, firstId], list.Items.Select(item => item.Id).ToArray());
        Assert.All(list.Items, item => Assert.Equal(NotificationSeverity.Info, item.Severity));
        Assert.Equal(HttpStatusCode.NotFound, otherUserResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, markRead.StatusCode);
        NotificationHistoryItem unreadItem = Assert.Single(unread.Items);
        Assert.Equal(firstId, unreadItem.Id);
        Assert.Equal(1, unread.TotalCount);
        Assert.Equal(1, unread.UnreadCount);
        Assert.Equal(1, markAll.UpdatedCount);
        Assert.Equal(0, afterMarkAll.UnreadCount);
        Assert.Equal(HttpStatusCode.Forbidden, tenantMismatch.StatusCode);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task User_broadcast_api_applies_audience_visibility_and_tenant_scoped_read_receipts()
    {
        await using NotificationsApiTestApplication application = await NotificationsApiTestApplication
            .CreateAsync();
        Guid tenantUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        Guid platformUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        Guid tenantAdminId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        Guid otherTenantUserId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        await application.AddBroadcastAsync(
                "tenant-a",
                DomainBroadcastAudience.TenantUsers,
                tenantUserId,
                "Tenant users",
                1);
        await application.AddBroadcastAsync(
                null,
                DomainBroadcastAudience.PlatformUsers,
                platformUserId,
                "Platform users",
                2);
        await application.AddBroadcastAsync(
                "tenant-a",
                DomainBroadcastAudience.TenantAdmins,
                tenantAdminId,
                "Tenant admins",
                3);
        await application.AddBroadcastAsync(
                "tenant-b",
                DomainBroadcastAudience.TenantUsers,
                otherTenantUserId,
                "Other tenant users",
                4);
        using HttpClient tenantAClient = CreateAuthenticatedClient(application, "tenant-a", "shared-user");

        NotificationBroadcastListResponse tenantAList = await GetJsonAsync<NotificationBroadcastListResponse>(
                tenantAClient,
                "/api/notifications/broadcasts?page=1&pageSize=10");
        using HttpResponseMessage adminOnly = await tenantAClient
            .GetAsync($"/api/notifications/broadcasts/{tenantAdminId}");
        using HttpResponseMessage markTenantRead = await tenantAClient
            .PostAsync($"/api/notifications/broadcasts/{tenantUserId}/read", content: null);
        NotificationBroadcastListResponse tenantAUnread = await GetJsonAsync<NotificationBroadcastListResponse>(
                tenantAClient,
                "/api/notifications/broadcasts?unreadOnly=true&page=1&pageSize=10");
        MarkAllNotificationBroadcastsReadResponse tenantAMarkAll =
            await PostJsonAsync<MarkAllNotificationBroadcastsReadResponse>(
                    tenantAClient,
                    "/api/notifications/broadcasts/read-all",
                    value: new { });
        NotificationBroadcastListResponse tenantAAfterMarkAll = await GetJsonAsync<NotificationBroadcastListResponse>(
                tenantAClient,
                "/api/notifications/broadcasts?page=1&pageSize=10");
        using HttpClient tenantBClient = CreateAuthenticatedClient(application, "tenant-b", "shared-user");
        NotificationBroadcastListResponse tenantBList = await GetJsonAsync<NotificationBroadcastListResponse>(
                tenantBClient,
                "/api/notifications/broadcasts?page=1&pageSize=10");

        Assert.Equal(2, tenantAList.TotalCount);
        Assert.Equal(2, tenantAList.UnreadCount);
        Assert.Equal(
            [platformUserId, tenantUserId],
            tenantAList.Items.Select(item => item.BroadcastId).ToArray());
        Assert.Equal(NotificationBroadcastAudience.PlatformUsers, tenantAList.Items[0].Audience);
        Assert.Equal(HttpStatusCode.NotFound, adminOnly.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, markTenantRead.StatusCode);
        NotificationBroadcastItem unreadBroadcast = Assert.Single(tenantAUnread.Items);
        Assert.Equal(platformUserId, unreadBroadcast.BroadcastId);
        Assert.Equal(1, tenantAMarkAll.UpdatedCount);
        Assert.Equal(0, tenantAAfterMarkAll.UnreadCount);
        Assert.Equal(2, tenantBList.TotalCount);
        Assert.Equal(2, tenantBList.UnreadCount);
        Assert.Contains(tenantBList.Items, item => item.BroadcastId == platformUserId);
        Assert.Contains(tenantBList.Items, item => item.BroadcastId == otherTenantUserId);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task User_broadcast_api_uses_default_tenant_scope_when_tenancy_is_disabled()
    {
        await using NotificationsApiTestApplication application = await NotificationsApiTestApplication
            .CreateAsync(tenancyEnabled: false);
        Guid defaultTenantBroadcastId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        Guid otherTenantBroadcastId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        Guid platformBroadcastId = Guid.Parse("99999999-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await application.AddBroadcastAsync(
                "default",
                DomainBroadcastAudience.TenantUsers,
                defaultTenantBroadcastId,
                "Default tenant users",
                1);
        await application.AddBroadcastAsync(
                "tenant-a",
                DomainBroadcastAudience.TenantUsers,
                otherTenantBroadcastId,
                "Other tenant users",
                2);
        await application.AddBroadcastAsync(
                null,
                DomainBroadcastAudience.PlatformUsers,
                platformBroadcastId,
                "Platform users",
                3);
        using HttpClient client = application.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            NotificationsApiTestApplication.CreateAccessToken("tenant-a", "shared-user"));

        NotificationBroadcastListResponse list = await GetJsonAsync<NotificationBroadcastListResponse>(
                client,
                "/api/notifications/broadcasts?page=1&pageSize=10");

        Assert.Equal(2, list.TotalCount);
        Assert.Contains(list.Items, item => item.BroadcastId == defaultTenantBroadcastId);
        Assert.Contains(list.Items, item => item.BroadcastId == platformBroadcastId);
        Assert.DoesNotContain(list.Items, item => item.BroadcastId == otherTenantBroadcastId);
    }

    private static HttpClient CreateAuthenticatedClient(
        NotificationsApiTestApplication application,
        string scopeId,
        string userId)
    {
        HttpClient client = application.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            NotificationsApiTestApplication.CreateAccessToken(scopeId, userId));
        client.DefaultRequestHeaders.Add("X-Tenant-Id", scopeId);
        return client;
    }

    private static async Task<T> GetJsonAsync<T>(HttpClient client, string requestUri)
    {
        using HttpResponseMessage response = await client.GetAsync(requestUri);
        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Response status code does not indicate success: {(int)response.StatusCode} ({response.StatusCode}). Body: {body}");
        }

        T? value = await response.Content.ReadFromJsonAsync<T>();
        Assert.NotNull(value);
        return value;
    }

    private static async Task<T> PostJsonAsync<T>(HttpClient client, string requestUri, object value)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(requestUri, value);
        response.EnsureSuccessStatusCode();
        T? result = await response.Content.ReadFromJsonAsync<T>();
        Assert.NotNull(result);
        return result;
    }
}
