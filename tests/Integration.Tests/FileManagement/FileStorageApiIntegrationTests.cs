namespace Integration.Tests;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Gma.Modules.Files.Contracts;
using Integration.Tests.Support;
using Xunit;

[Trait("Category", "Integration")]
public sealed class FileStorageApiIntegrationTests
{
    [Fact]
    public async Task Files_api_enforces_subject_and_tenant_scope()
    {
        await using FilesApiTestApplication application = await FilesApiTestApplication.CreateAsync();
        using HttpClient ownerClient = CreateAuthenticatedClient(application, "tenant-a", "user-a");
        using HttpClient otherUserClient = CreateAuthenticatedClient(application, "tenant-a", "user-b");
        using HttpClient tenantMismatchClient = CreateAuthenticatedClient(application, "tenant-b", "user-a");
        tenantMismatchClient.DefaultRequestHeaders.Remove("X-Tenant-Id");
        tenantMismatchClient.DefaultRequestHeaders.Add("X-Tenant-Id", "tenant-a");

        FileUploadResponse upload = await UploadTextAsync(ownerClient, "hello-files-api");
        using HttpResponseMessage ownerDownload = await ownerClient.GetAsync(upload.DownloadPath);
        string ownerContent = await ownerDownload.Content.ReadAsStringAsync();
        using HttpResponseMessage otherUserDownload = await otherUserClient.GetAsync(upload.DownloadPath);
        using HttpResponseMessage tenantMismatchDownload = await tenantMismatchClient.GetAsync(upload.DownloadPath);
        using HttpResponseMessage otherUserDelete = await otherUserClient.DeleteAsync(upload.DownloadPath);
        using HttpResponseMessage ownerDelete = await ownerClient.DeleteAsync(upload.DownloadPath);

        Assert.Equal(HttpStatusCode.OK, ownerDownload.StatusCode);
        Assert.Equal("hello-files-api", ownerContent);
        Assert.Equal(HttpStatusCode.NotFound, otherUserDownload.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, tenantMismatchDownload.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otherUserDelete.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, ownerDelete.StatusCode);
    }

    private static HttpClient CreateAuthenticatedClient(
        FilesApiTestApplication application,
        string scopeId,
        string userId)
    {
        HttpClient client = application.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            FilesApiTestApplication.CreateAccessToken(scopeId, userId));
        client.DefaultRequestHeaders.Add("X-Tenant-Id", scopeId);
        return client;
    }

    private static async Task<FileUploadResponse> UploadTextAsync(HttpClient client, string content)
    {
        using MultipartFormDataContent form = new();
        using ByteArrayContent fileContent = new(Encoding.UTF8.GetBytes(content));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(fileContent, "file", "api.txt");

        using HttpResponseMessage response = await client.PostAsync("/api/files", form);
        response.EnsureSuccessStatusCode();
        FileUploadResponse? upload = await response.Content.ReadFromJsonAsync<FileUploadResponse>();

        Assert.NotNull(upload);
        return upload;
    }
}
