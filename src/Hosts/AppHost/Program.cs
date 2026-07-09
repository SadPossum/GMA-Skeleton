using Aspire.Hosting.ApplicationModel;

var builder = DistributedApplication.CreateBuilder(args);

var sqlServer = builder.AddSqlServer("sql")
    .AddDatabase("SqlServer");

var postgreSql = builder.AddPostgres("postgres")
    .AddDatabase("PostgreSql");

var nats = builder.AddNats("nats")
    .WithJetStream()
    .WithDataVolume(isReadOnly: false);

bool workerEnabled = bool.TryParse(
    builder.Configuration["AppHost:Worker:Enabled"],
    out bool configuredWorkerEnabled) && configuredWorkerEnabled;

var api = builder.AddProject<Projects.Host_Api>("host-api")
    .WithReference(sqlServer)
    .WithReference(postgreSql)
    .WithReference(nats)
    .WaitFor(nats)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("NatsJetStream__Enabled", workerEnabled ? "false" : "true");

bool adminApiEnabled = bool.TryParse(
    builder.Configuration["AppHost:AdminApi:Enabled"],
    out bool configuredAdminApiEnabled) && configuredAdminApiEnabled;

IResourceBuilder<ProjectResource>? adminApi = null;
if (adminApiEnabled)
{
    adminApi = builder.AddProject<Projects.Host_AdminApi>("host-admin-api")
        .WithReference(sqlServer)
        .WithReference(postgreSql)
        .WithReference(nats)
        .WaitFor(nats)
        .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
        .WithEnvironment("NatsJetStream__Enabled", workerEnabled ? "false" : "true");
}

IResourceBuilder<ProjectResource>? worker = null;
if (workerEnabled)
{
    worker = builder.AddProject<Projects.Host_Worker>("host-worker")
        .WithReference(sqlServer)
        .WithReference(postgreSql)
        .WithReference(nats)
        .WaitFor(nats)
        .WithEnvironment("DOTNET_ENVIRONMENT", "Development")
        .WithEnvironment("NatsJetStream__Enabled", "true")
        .WithEnvironment("NatsConsumers__Enabled", "false")
        .WithEnvironment("Tasks__Worker__Enabled", "false")
        .WithEnvironment("Worker__Modules__Auth", "true");
}

bool redisEnabled = bool.TryParse(
    builder.Configuration["AppHost:Redis:Enabled"],
    out bool configuredRedisEnabled) && configuredRedisEnabled;

if (redisEnabled)
{
    var redis = builder.AddRedis("redis");
    api.WithReference(redis)
        .WaitFor(redis)
        .WithEnvironment("Caching__Enabled", "true")
        .WithEnvironment("Caching__Provider", "Redis");

    if (adminApi is { } configuredAdminApi)
    {
        configuredAdminApi.WithReference(redis)
            .WaitFor(redis)
            .WithEnvironment("Caching__Enabled", "true")
            .WithEnvironment("Caching__Provider", "Redis");
    }

    if (worker is { } configuredWorker)
    {
        configuredWorker.WithReference(redis)
            .WaitFor(redis)
            .WithEnvironment("Caching__Enabled", "true")
            .WithEnvironment("Caching__Provider", "Redis");
    }
}

builder.Build().Run();
