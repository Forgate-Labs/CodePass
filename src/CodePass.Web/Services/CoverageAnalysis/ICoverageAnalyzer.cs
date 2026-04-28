namespace CodePass.Web.Services.CoverageAnalysis;

public interface ICoverageAnalyzer
{
    Task<CoverageAnalysisResult> AnalyzeAsync(
        string solutionPath,
        CancellationToken cancellationToken = default,
        IProgress<CoverageAnalysisProgressDto>? progress = null);
}
