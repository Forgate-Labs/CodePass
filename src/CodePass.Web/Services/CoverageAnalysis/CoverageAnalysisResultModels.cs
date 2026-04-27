using CodePass.Web.Data.Entities;

namespace CodePass.Web.Services.CoverageAnalysis;

public sealed record CoverageAnalysisRunDto(
    Guid Id,
    Guid RegisteredSolutionId,
    CoverageAnalysisRunStatus Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    int ProjectCount,
    int ClassCount,
    int CoveredLineCount,
    int TotalLineCount,
    double LineCoveragePercent,
    int CoveredBranchCount,
    int TotalBranchCount,
    double BranchCoveragePercent,
    string? ErrorMessage,
    IReadOnlyList<CoverageProjectSummaryDto> ProjectSummaries,
    IReadOnlyList<CoverageClassCoverageDto> ClassCoverages);

public sealed record CoverageProjectSummaryDto(
    Guid Id,
    string ProjectName,
    int CoveredLineCount,
    int TotalLineCount,
    double LineCoveragePercent,
    int CoveredBranchCount,
    int TotalBranchCount,
    double BranchCoveragePercent);

public sealed record CoverageClassCoverageDto(
    Guid Id,
    string ProjectName,
    string ClassName,
    string FilePath,
    int CoveredLineCount,
    int TotalLineCount,
    double LineCoveragePercent,
    int CoveredBranchCount,
    int TotalBranchCount,
    double BranchCoveragePercent);
