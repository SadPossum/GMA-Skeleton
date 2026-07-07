namespace TaskSamples.Application.Tasks;

using Gma.Framework.Cqrs;

public sealed record RecordTaskSampleReportCommand(
    string ReportName,
    int ExpectedRows,
    Guid RunId,
    string TenantId,
    int Attempt) : ICommand<Unit>;
