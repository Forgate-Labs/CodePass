using CodePass.Web.Services.Solutions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace CodePass.Web.Services.AgentAnalysis;

public static class AgentQualityAnalysisEndpoints
{
    public static IEndpointRouteBuilder MapAgentQualityAnalysisEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/agent-quality")
            .WithTags("Agent quality analysis");

        group.MapGet("/solutions", ListSolutionsAsync)
            .WithName("ListAgentQualityAnalysisSolutions")
            .WithSummary("Lists registered solutions available for local AI agents.")
            .WithDescription("Returns registered solution identifiers and metadata so an agent can request a quality analysis.");

        group.MapPost("/solutions/{registeredSolutionId:guid}/analyze", AnalyzeAsync)
            .WithName("RunAgentQualityAnalysis")
            .WithSummary("Runs project quality analyses for a local AI agent.")
            .WithDescription("Executes the administrator-defined rule analysis and/or test coverage analysis, then returns the current quality score summary.");

        return endpoints;
    }

    private static async Task<Ok<IReadOnlyList<AgentRegisteredSolutionDto>>> ListSolutionsAsync(
        IRegisteredSolutionService registeredSolutionService,
        CancellationToken cancellationToken)
    {
        var solutions = (await registeredSolutionService.GetAllAsync(cancellationToken))
            .Select(solution => solution.ToAgentDto())
            .OrderBy(solution => solution.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return TypedResults.Ok<IReadOnlyList<AgentRegisteredSolutionDto>>(solutions);
    }

    private static async Task<Results<Ok<AgentQualityAnalysisResponse>, NotFound<ProblemDetails>, BadRequest<ProblemDetails>>> AnalyzeAsync(
        Guid registeredSolutionId,
        [FromBody] AgentQualityAnalysisRequest? request,
        IAgentQualityAnalysisService analysisService,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await analysisService.AnalyzeAsync(
                registeredSolutionId,
                request ?? new AgentQualityAnalysisRequest(),
                cancellationToken);

            return TypedResults.Ok(response);
        }
        catch (KeyNotFoundException exception)
        {
            return TypedResults.NotFound(new ProblemDetails
            {
                Title = "Registered solution not found",
                Detail = exception.Message,
                Status = StatusCodes.Status404NotFound
            });
        }
        catch (InvalidOperationException exception)
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Invalid analysis request",
                Detail = exception.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }
}
