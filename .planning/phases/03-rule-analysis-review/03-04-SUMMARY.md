---
phase: 03-rule-analysis-review
plan: 04
subsystem: rule-analysis
tags: [dotnet, blazor, roslyn, ef-core, manual-runs, xunit]
requires:
  - phase: 01-registered-solutions
    provides: registered solution persistence and validity status used to gate manual analysis runs
  - phase: 03-rule-analysis-review
    provides: per-solution authored-rule selection, Roslyn analyzer execution, and persisted grouped result services
provides:
  - Manual rule-analysis run orchestration service for UI callers
  - Scoped DI registrations for the Roslyn analyzer, result persistence, and manual run service
  - Regression tests for selected-rule execution, success persistence, failure persistence, zero-rule runs, and DI resolution
affects: [03-05, 03-06, rule-analysis-ui, quality-dashboard]
tech-stack:
  added: []
  patterns: [thin orchestration service, persisted failed runs for non-valid solutions, zero-rule success path, fake analyzer orchestration tests]
key-files:
  created:
    - src/CodePass.Web/Services/RuleAnalysis/IRuleAnalysisRunService.cs
    - src/CodePass.Web/Services/RuleAnalysis/RuleAnalysisRunService.cs
    - tests/CodePass.Web.Tests/Services/RuleAnalysisRunServiceTests.cs
  modified:
    - src/CodePass.Web/Program.cs
key-decisions:
  - "Expose manual rule-analysis execution through a scoped IRuleAnalysisRunService so UI callers do not compose selection, analyzer, and result persistence services directly."
  - "Treat non-valid registered solutions as persisted failed runs with readable errors, while unknown solution ids remain clear InvalidOperationException failures."
  - "Complete valid zero-rule runs successfully without invoking the analyzer so authored-rule selection can be empty without breaking manual execution."
patterns-established:
  - "Manual rule-analysis orchestration loads the registered solution, resolves enabled user-authored rules, persists a run, executes the analyzer, and completes or fails through RuleAnalysisResultService."
  - "Orchestration tests use real EF in-memory persistence and a fake IRuleAnalyzer to prove backend behavior without loading Roslyn workspaces."
requirements-completed: [RULE-01, RULE-04, RULE-05, RULE-06]
duration: 2 min
completed: 2026-04-25
---

# Phase 03 Plan 04: Manual Rule Analysis Run Orchestration Summary

**Scoped manual rule-analysis orchestration from selected user-authored rules through Roslyn execution to persisted grouped run results.**

## Performance

- **Duration:** 2 min
- **Started:** 2026-04-25T04:00:10Z
- **Completed:** 2026-04-25T04:02:31Z
- **Tasks:** 3
- **Files modified:** 4

## Accomplishments
- Added `IRuleAnalysisRunService` and `RuleAnalysisRunService` as the single backend entry point for manual rule-analysis runs.
- Wired manual runs to registered-solution validation, per-solution enabled authored-rule selection, analyzer execution, and persisted success/failure results.
- Registered the Roslyn analyzer and manual run service in scoped dependency injection while preserving existing authored-rule and solution services.
- Added regression coverage for selected-rule filtering, grouped success persistence, zero-rule success, non-valid solution failure, analyzer exception failure, and scoped DI resolution.

## Task Commits

Each task was committed atomically:

1. **Task 1: Create manual run orchestration service** - `06a9d40` (feat)
2. **Task 2: Register analysis services in DI** - `e7cffa7` (feat)
3. **Task 3: Test run orchestration outcomes** - `43511ba` (test)

## Verification

- `dotnet build CodePass.sln` passed after Task 1.
- `dotnet build CodePass.sln` passed after Task 2.
- `dotnet test CodePass.sln --filter "FullyQualifiedName~RuleAnalysisRunServiceTests"` passed after Task 3.
- Final verification passed: `dotnet test CodePass.sln --filter "FullyQualifiedName~RuleAnalysisRunServiceTests" && dotnet build CodePass.sln`.

## Files Created/Modified
- `src/CodePass.Web/Services/RuleAnalysis/IRuleAnalysisRunService.cs` - Defines `StartRunAsync` as the manual analysis entry point for UI callers.
- `src/CodePass.Web/Services/RuleAnalysis/RuleAnalysisRunService.cs` - Coordinates registered solution lookup, status validation, enabled authored-rule loading, analyzer execution, and persisted result completion/failure.
- `src/CodePass.Web/Program.cs` - Registers `IRuleAnalyzer`, `IRuleAnalysisResultService`, and `IRuleAnalysisRunService` as scoped services alongside existing solution and rule services.
- `tests/CodePass.Web.Tests/Services/RuleAnalysisRunServiceTests.cs` - Covers selected authored-rule execution, globally disabled filtering, grouped result persistence, zero-rule runs, non-valid solution failures, analyzer exception failures, and DI resolution.

## Decisions Made
- Exposed manual analysis through `IRuleAnalysisRunService` so UI work in Plan 03-05 has a single backend operation to call.
- Persisted non-valid registered solution attempts as failed runs with readable status messages instead of throwing, while still throwing clearly for unknown solution ids.
- Completed valid runs with no enabled authored rules as succeeded zero-violation runs and skipped analyzer invocation for that empty-input path.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## Authentication Gates

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Plan `03-05` can call `IRuleAnalysisRunService.StartRunAsync` from the `/analysis/rules` UI to trigger manual analysis for a selected registered solution.
- Grouped run DTOs returned from orchestration are ready for the UI to display rule groups, severities, violation counts, and source locations.
- No built-in production rule provider or sample rule pack was registered; analysis remains user-authored-rule-only.

## Self-Check: PASSED

- Verified all key implementation/test files listed in this summary exist on disk.
- Verified task commits `06a9d40`, `e7cffa7`, and `43511ba` exist in git history.

---
*Phase: 03-rule-analysis-review*
*Completed: 2026-04-25*
