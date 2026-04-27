---
phase: 04-coverage-analysis-review
plan: 04
subsystem: coverage-analysis-orchestration
tags: [dotnet, blazor, coverage-analysis, manual-runs, ef-core, xunit]
requires:
  - phase: 01-registered-solutions
    provides: registered solution persistence and validity status used to gate manual coverage runs
  - phase: 04-coverage-analysis-review
    provides: coverage analyzer execution and coverage result persistence services from Plans 04-01 through 04-03
provides:
  - Manual coverage-analysis run orchestration service for UI callers
  - Scoped DI registrations for the dotnet coverage analyzer and manual coverage run service
  - Regression tests for successful coverage persistence, invalid solution failure, unknown id errors, analyzer failures, and DI resolution
affects: [04-05, 04-06, coverage-analysis-ui, quality-score-dashboard]
tech-stack:
  added: []
  patterns: [thin orchestration service, persisted failed runs for non-valid coverage targets, fake analyzer orchestration tests]
key-files:
  created:
    - src/CodePass.Web/Services/CoverageAnalysis/ICoverageAnalysisRunService.cs
    - src/CodePass.Web/Services/CoverageAnalysis/CoverageAnalysisRunService.cs
    - tests/CodePass.Web.Tests/Services/CoverageAnalysisRunServiceTests.cs
  modified:
    - src/CodePass.Web/Program.cs
key-decisions:
  - "Expose manual coverage-analysis execution through a scoped ICoverageAnalysisRunService so UI callers do not compose registered-solution lookup, analyzer execution, and result persistence directly."
  - "Treat non-valid registered solutions as persisted failed coverage runs with readable status messages, while unknown solution ids remain clear InvalidOperationException failures."
  - "Keep coverage process execution inside DotNetCoverageAnalyzer and result storage inside CoverageAnalysisResultService; the run service remains a thin orchestrator with no scheduling or CI trigger."
patterns-established:
  - "Manual coverage orchestration loads the registered solution, creates a running run, executes ICoverageAnalyzer only for valid solutions, and completes or fails through CoverageAnalysisResultService."
  - "Coverage orchestration tests use real EF in-memory persistence and a fake ICoverageAnalyzer to prove backend behavior without invoking dotnet test."
requirements-completed: [COV-01, COV-02, COV-03, COV-04]
duration: 2 min
completed: 2026-04-27
---

# Phase 04 Plan 04: Manual Coverage Analysis Run Orchestration Summary

**Scoped manual coverage-analysis orchestration from registered solution selection through dotnet coverage execution to persisted project and class coverage results.**

## Performance

- **Duration:** 2 min
- **Started:** 2026-04-27T19:02:51Z
- **Completed:** 2026-04-27T19:05:34Z
- **Tasks:** 3
- **Files modified:** 4

## Accomplishments

- Added `ICoverageAnalysisRunService` as the single backend entry point for manual coverage runs from the UI.
- Implemented `CoverageAnalysisRunService` to load registered solutions, create durable running runs, gate non-valid solution statuses, execute `ICoverageAnalyzer`, and mark runs succeeded or failed through the result service.
- Registered `ICoverageAnalyzer`, `ICoverageAnalysisResultService`, and `ICoverageAnalysisRunService` as scoped DI services while preserving the existing rule-analysis registrations.
- Added regression tests covering successful analyzer invocation and normalized persistence, invalid solution failures without analyzer calls, unknown solution id errors, analyzer exception persistence, and scoped DI resolution.

## Task Commits

Each task was committed atomically:

1. **Task 1: Create manual coverage run orchestration service** - `d4bf67e` (feat)
2. **Task 2: Register coverage analyzer and run services in scoped DI** - `4de1cd6` (feat)
3. **Task 3: Test manual coverage run orchestration outcomes** - `bf02049` (test)

**Plan metadata:** Pending final docs commit.

## Verification

- Task 1: `dotnet build CodePass.sln` passed.
- Task 2: `dotnet build CodePass.sln` passed.
- Task 3: `dotnet test CodePass.sln --filter "FullyQualifiedName~CoverageAnalysisRunServiceTests" && dotnet build CodePass.sln` passed with 5 targeted tests.
- Final verification passed: `dotnet test CodePass.sln --filter "FullyQualifiedName~CoverageAnalysisRunServiceTests" && dotnet build CodePass.sln`.

## Files Created/Modified

- `src/CodePass.Web/Services/CoverageAnalysis/ICoverageAnalysisRunService.cs` - Defines `StartRunAsync` as the manual coverage-analysis entry point for UI callers.
- `src/CodePass.Web/Services/CoverageAnalysis/CoverageAnalysisRunService.cs` - Coordinates registered solution lookup, status validation, analyzer execution, and persisted success/failure results.
- `src/CodePass.Web/Program.cs` - Registers `ICoverageAnalyzer`, `ICoverageAnalysisResultService`, and `ICoverageAnalysisRunService` as scoped services alongside existing rule-analysis services.
- `tests/CodePass.Web.Tests/Services/CoverageAnalysisRunServiceTests.cs` - Covers success, invalid solution, unknown id, analyzer failure, and DI resolution outcomes using EF in-memory persistence and a fake analyzer.

## Decisions Made

- Exposed manual coverage analysis through `ICoverageAnalysisRunService` so Plan `04-05` can trigger coverage with one scoped backend operation.
- Persisted non-valid registered solution attempts as failed coverage runs with readable status messages instead of throwing, while still throwing clearly for unknown solution ids.
- Kept the orchestrator thin: process execution remains in `DotNetCoverageAnalyzer`, result lifecycle persistence remains in `CoverageAnalysisResultService`, and no automated scheduling, CI integration, or scoring was introduced.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## Authentication Gates

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Plan `04-05` can inject `ICoverageAnalysisRunService` in the `/analysis/coverage` UI to start manual coverage runs for a selected registered solution.
- Coverage UI work can consume the returned `CoverageAnalysisRunDto` for run status, readable errors, project summaries, and class coverage rows.
- The quality dashboard phase can later rely on manually refreshed latest coverage runs without any background scheduler or CI trigger.

## Self-Check: PASSED

- Verified all key implementation/test files listed in this summary exist on disk.
- Verified task commits `d4bf67e`, `4de1cd6`, and `bf02049` exist in git history.
- Verified final targeted orchestration test and build command completed successfully.

---
*Phase: 04-coverage-analysis-review*
*Completed: 2026-04-27*
