namespace CodePass.Web.Data.Entities;

public sealed class CoverageAnalysisRun
{
    public Guid Id { get; set; }

    public Guid RegisteredSolutionId { get; set; }

    public CoverageAnalysisRunStatus Status { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public int ProjectCount { get; set; }

    public int ClassCount { get; set; }

    public int CoveredLineCount { get; set; }

    public int TotalLineCount { get; set; }

    public double LineCoveragePercent { get; set; }

    public int CoveredBranchCount { get; set; }

    public int TotalBranchCount { get; set; }

    public double BranchCoveragePercent { get; set; }

    public string? ErrorMessage { get; set; }

    public ICollection<CoverageProjectSummary> ProjectSummaries { get; } = new List<CoverageProjectSummary>();

    public ICollection<CoverageClassCoverage> ClassCoverages { get; } = new List<CoverageClassCoverage>();
}
