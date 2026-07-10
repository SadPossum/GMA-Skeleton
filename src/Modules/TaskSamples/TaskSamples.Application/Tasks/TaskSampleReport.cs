namespace TaskSamples.Application.Tasks;

public sealed record TaskSampleReport(
    string ReportName,
    int ExpectedRows,
    Guid RunId,
    string ScopeId,
    int Attempt);
