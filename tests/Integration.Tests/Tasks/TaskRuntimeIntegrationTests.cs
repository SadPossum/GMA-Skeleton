namespace Integration.Tests;

using System.Text.Json;
using Gma.Framework.Results;
using Gma.Framework.Tasks;
using Integration.Tests.Support;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class TaskRuntimeIntegrationTests
{
    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Task_worker_processes_sample_task_through_persisted_runtime()
    {
        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("gma_task_worker_tests")
            .Build();
        await postgreSql.StartAsync();

        await using TaskRuntimeTestApplication application = new(
            "PostgreSql",
            postgreSql.GetConnectionString(),
            workerEnabled: true);
        await application.MigrateDatabaseAsync().ConfigureAwait(false);
        Guid runId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

        await application.EnqueueSampleTaskAsync(runId, DateTimeOffset.UtcNow).ConfigureAwait(false);
        await application.StartAsync().ConfigureAwait(false);

        IReadOnlyList<TaskSamples.Application.Tasks.TaskSampleReport> reports =
            await application.Sink.WaitForReportsAsync(1, TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        TaskRunSnapshot snapshot = await application.WaitForStatusAsync(
                runId,
                TaskRunStatus.Succeeded,
                TimeSpan.FromSeconds(10))
            .ConfigureAwait(false);

        Assert.Single(reports);
        Assert.Equal(runId, reports[0].RunId);
        Assert.Equal("tenant-a", reports[0].ScopeId);
        Assert.Equal(TaskRunStatus.Succeeded, snapshot.Status);
        Assert.Equal(1, snapshot.Attempts);
        Assert.Null(snapshot.LockedBy);
    }

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Task_worker_cooperatively_cancels_slow_task_from_control_message()
    {
        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("gma_task_control_tests")
            .Build();
        await postgreSql.StartAsync();

        await using TaskRuntimeTestApplication application = new(
            "PostgreSql",
            postgreSql.GetConnectionString(),
            workerEnabled: true);
        await application.MigrateDatabaseAsync().ConfigureAwait(false);
        Guid runId = Guid.Parse("12121212-3434-5656-7878-909090909090");
        string payload = JsonSerializer.Serialize(new TaskSamples.Contracts.SlowReportTaskPayload(
            "slow",
            10,
            Steps: 20,
            DelayMilliseconds: 50));

        await application.EnqueueSampleTaskAsync(
                runId,
                DateTimeOffset.UtcNow,
                maxAttempts: 1,
                payloadJson: payload,
                taskName: TaskSamples.Contracts.SlowReportTaskPayload.TaskName)
            .ConfigureAwait(false);
        await application.StartAsync().ConfigureAwait(false);
        Result<TaskControlMessage> control = await application.SendControlThroughApplicationAsync(
                runId,
                TaskControlCommandNames.Cancel,
                "{}",
                DateTimeOffset.UtcNow.AddMinutes(5))
            .ConfigureAwait(false);
        TaskRunSnapshot snapshot = await application.WaitForStatusAsync(
                runId,
                TaskRunStatus.Canceled,
                TimeSpan.FromSeconds(10))
            .ConfigureAwait(false);

        Assert.True(control.IsSuccess);
        Assert.True(
            snapshot.Status == TaskRunStatus.Canceled,
            $"Expected a canceled run, but observed {snapshot.Status}: {snapshot.LastError}");
        Assert.Empty(application.Sink.Reports);
    }
}
