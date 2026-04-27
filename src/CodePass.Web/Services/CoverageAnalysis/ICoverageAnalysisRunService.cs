namespace CodePass.Web.Services.CoverageAnalysis;

public interface ICoverageAnalysisRunService
{
    Task<CoverageAnalysisRunDto> StartRunAsync(Guid registeredSolutionId, CancellationToken cancellationToken = default);
}
