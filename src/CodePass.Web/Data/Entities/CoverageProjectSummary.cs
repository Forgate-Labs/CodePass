namespace CodePass.Web.Data.Entities;

public sealed class CoverageProjectSummary
{
    public Guid Id { get; set; }

    public Guid CoverageAnalysisRunId { get; set; }

    public required string ProjectName { get; set; }

    public int CoveredLineCount { get; set; }

    public int TotalLineCount { get; set; }

    public double LineCoveragePercent { get; set; }

    public int CoveredBranchCount { get; set; }

    public int TotalBranchCount { get; set; }

    public double BranchCoveragePercent { get; set; }

    public CoverageAnalysisRun? CoverageAnalysisRun { get; set; }
}
