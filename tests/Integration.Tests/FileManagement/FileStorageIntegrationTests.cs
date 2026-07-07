namespace Integration.Tests;

using System.Text;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Gma.Modules.Files.Api;
using Gma.Modules.Files.Application.Commands;
using Gma.Modules.Files.Application.Queries;
using Gma.Modules.Files.Application.ReadModels;
using Gma.Modules.Files.Contracts;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Gma.Framework.AccessControl;
using Gma.Framework.Api.Modules;
using Gma.Framework.Cqrs;
using Gma.Framework.Cqrs.Infrastructure;
using Gma.Framework.FileManagement;
using Gma.Framework.FileManagement.LocalStorage;
using Gma.Framework.FileManagement.Minio;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Infrastructure;
using Gma.Framework.Tenancy.Infrastructure;
using Xunit;

[Trait("Category", "Integration")]
public sealed class FileStorageIntegrationTests
{
    [Fact]
    public async Task Files_module_round_trips_upload_download_and_delete_with_local_storage()
    {
        string root = Path.Combine(Path.GetTempPath(), $"gma-files-module-{Guid.NewGuid():N}");

        try
        {
            using ServiceProvider provider = BuildFilesProvider(root);
            using IServiceScope scope = provider.CreateScope();
            IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
            FormOptions formOptions = scope.ServiceProvider.GetRequiredService<IOptions<FormOptions>>().Value;
            byte[] payload = Encoding.UTF8.GetBytes("module-upload");

            Assert.Equal(1048576, formOptions.MultipartBodyLengthLimit);

            await using MemoryStream uploadContent = new(payload);
            Result<FileUploadResponse> upload = await dispatcher.SendAsync(new UploadFileCommand(
                uploadContent,
                payload.Length,
                "text/plain",
                "profile.txt",
                UserSubject("user-a")));

            Assert.True(upload.IsSuccess, upload.Error.Message);
            Assert.Equal("text/plain", upload.Value.ContentType);
            Assert.Equal(payload.Length, upload.Value.ContentLength);
            Assert.Equal("profile.txt", upload.Value.FileName);

            Result<FileDownload> download = await dispatcher.QueryAsync(
                new GetFileQuery(upload.Value.FileId, UserSubject("user-a")));
            Assert.True(download.IsSuccess, download.Error.Message);

            await using MemoryStream downloaded = new();
            await download.Value.File.CopyToAsync(downloaded);

            Assert.Equal(payload, downloaded.ToArray());

            Result<Unit> delete = await dispatcher.SendAsync(
                new DeleteFileCommand(upload.Value.FileId, UserSubject("user-a")));
            Assert.True(delete.IsSuccess, delete.Error.Message);

            Result<FileDownload> afterDelete = await dispatcher.QueryAsync(
                new GetFileQuery(upload.Value.FileId, UserSubject("user-a")));
            Assert.True(afterDelete.IsFailure);
            Assert.Equal(Gma.Modules.Files.Application.FilesApplicationErrors.FileNotFound, afterDelete.Error);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Files_module_keeps_user_objects_isolated_with_local_storage()
    {
        string root = Path.Combine(Path.GetTempPath(), $"gma-files-module-{Guid.NewGuid():N}");

        try
        {
            using ServiceProvider provider = BuildFilesProvider(root);
            using IServiceScope scope = provider.CreateScope();
            IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
            byte[] payload = Encoding.UTF8.GetBytes("private-user-upload");

            await using MemoryStream uploadContent = new(payload);
            Result<FileUploadResponse> upload = await dispatcher.SendAsync(new UploadFileCommand(
                uploadContent,
                payload.Length,
                "text/plain",
                "private.txt",
                UserSubject("user-a")));

            Assert.True(upload.IsSuccess, upload.Error.Message);

            Result<FileDownload> otherUserDownload = await dispatcher.QueryAsync(
                new GetFileQuery(upload.Value.FileId, UserSubject("user-b")));
            Result<Unit> otherUserDelete = await dispatcher.SendAsync(
                new DeleteFileCommand(upload.Value.FileId, UserSubject("user-b")));
            Result<FileDownload> ownerDownload = await dispatcher.QueryAsync(
                new GetFileQuery(upload.Value.FileId, UserSubject("user-a")));

            Assert.True(otherUserDownload.IsFailure);
            Assert.Equal(Gma.Modules.Files.Application.FilesApplicationErrors.FileNotFound, otherUserDownload.Error);
            Assert.True(otherUserDelete.IsFailure);
            Assert.Equal(Gma.Modules.Files.Application.FilesApplicationErrors.FileNotFound, otherUserDelete.Error);
            Assert.True(ownerDownload.IsSuccess, ownerDownload.Error.Message);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [DockerFact]
    [Trait("Category", "Docker")]
    public async Task Minio_adapter_round_trips_storage_contract_against_container()
    {
        const string accessKey = "minioadmin";
        const string secretKey = "minioadmin";
        string bucketName = $"gma-file-tests-{Guid.NewGuid():N}";

        await using IContainer minio = new ContainerBuilder("quay.io/minio/minio:latest")
            .WithEnvironment("MINIO_ROOT_USER", accessKey)
            .WithEnvironment("MINIO_ROOT_PASSWORD", secretKey)
            .WithPortBinding(9000, assignRandomHostPort: true)
            .WithCommand("server", "/data", "--console-address", ":9001")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(9000))
            .Build();

        await minio.StartAsync().ConfigureAwait(false);

        using ServiceProvider provider = BuildMinioProvider(
            $"localhost:{minio.GetMappedPublicPort(9000)}",
            accessKey,
            secretKey,
            bucketName);

        IFileStorage storage = provider.GetRequiredService<IFileStorage>();
        FileStorageObjectKey key = new($"integration/{Guid.NewGuid():N}");
        byte[] payload = Encoding.UTF8.GetBytes("minio-upload");

        await using MemoryStream input = new(payload);
        FileStorageObjectProperties stored = await storage.PutAsync(new FileStorageWriteRequest(
            key,
            input,
            payload.Length,
            "text/plain",
            "minio.txt",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["module"] = "integration-tests"
            }));

        Assert.Equal(payload.Length, stored.ContentLength);
        Assert.Equal("text/plain", stored.ContentType);

        FileStorageReadResult? read = await storage.OpenReadAsync(key);
        Assert.NotNull(read);

        await using MemoryStream output = new();
        await read.CopyToAsync(output);

        Assert.Equal(payload, output.ToArray());
        Assert.True(await storage.DeleteAsync(key));
        Assert.Null(await storage.GetPropertiesAsync(key));
    }

    private static ServiceProvider BuildFilesProvider(string root)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Tenancy:Enabled"] = "false";
        builder.Configuration["FileManagement:Enabled"] = "true";
        builder.Configuration["FileManagement:Provider"] = "LocalStorage";
        builder.Configuration["FileManagement:MaximumObjectBytes"] = "1048576";
        builder.Configuration["FileManagement:AllowedContentTypes:0"] = "text/plain";
        builder.Configuration["FileManagement:LocalStorage:RootPath"] = root;
        builder.AddTenancyInfrastructure();
        builder.AddRuntimeInfrastructure();
        builder.AddCqrsInfrastructure();
        builder.AddLocalFileStorage();
        builder.AddModule<FilesModule>();
        builder.ValidateModuleComposition();
        return builder.Services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static ServiceProvider BuildMinioProvider(
        string endpoint,
        string accessKey,
        string secretKey,
        string bucketName)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["FileManagement:Enabled"] = "true";
        builder.Configuration["FileManagement:Provider"] = "Minio";
        builder.Configuration["FileManagement:MaximumObjectBytes"] = "1048576";
        builder.Configuration["FileManagement:Minio:Endpoint"] = endpoint;
        builder.Configuration["FileManagement:Minio:AccessKey"] = accessKey;
        builder.Configuration["FileManagement:Minio:SecretKey"] = secretKey;
        builder.Configuration["FileManagement:Minio:BucketName"] = bucketName;
        builder.Configuration["FileManagement:Minio:UseSsl"] = "false";
        builder.Configuration["FileManagement:Minio:CreateBucketIfMissing"] = "true";
        builder.AddMinioFileStorage();
        return builder.Services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static AccessSubject UserSubject(string userId) => AccessSubject.User(userId, tenantId: null);
}
