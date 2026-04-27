namespace CodePass.Web.Services.CoverageAnalysis;

public sealed record CoverageAnalysisResult(
    IReadOnlyList<CoverageProjectSummary> Projects,
    IReadOnlyList<CoverageClassCoverage> Classes);

public sealed record CoverageProjectSummary(
    string ProjectName,
    int CoveredLines,
    int TotalLines,
    double LineCoveragePercent,
    int CoveredBranches,
    int TotalBranches,
    double BranchCoveragePercent);

public sealed record CoverageClassCoverage(
    string ProjectName,
    string ClassName,
    string FilePath,
    int CoveredLines,
    int TotalLines,
    double LineCoveragePercent,
    int CoveredBranches,
    int TotalBranches,
    double BranchCoveragePercent);
