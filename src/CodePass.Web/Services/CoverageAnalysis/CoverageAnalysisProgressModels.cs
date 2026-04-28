namespace CodePass.Web.Services.CoverageAnalysis;

public enum CoverageAnalysisProgressStage
{
    Preparing,
    CreatingRun,
    RunningTests,
    CollectingCoverage,
    ParsingCoverage,
    SavingResults,
    Completed,
    Failed
}

public sealed record CoverageAnalysisProgressDto
{
    public CoverageAnalysisProgressDto(
        CoverageAnalysisProgressStage stage,
        string message,
        int? PercentComplete = null,
        int? Current = null,
        int? Total = null,
        string? Detail = null,
        DateTimeOffset? UpdatedAtUtc = null)
    {
        Stage = stage;
        Message = message;
        this.PercentComplete = PercentComplete;
        this.Current = Current;
        this.Total = Total;
        this.Detail = Detail;
        this.UpdatedAtUtc = UpdatedAtUtc ?? DateTimeOffset.UtcNow;
    }

    public CoverageAnalysisProgressStage Stage { get; }

    public string Message { get; }

    public int? PercentComplete { get; }

    public int? Current { get; }

    public int? Total { get; }

    public string? Detail { get; }

    public DateTimeOffset UpdatedAtUtc { get; }
}
