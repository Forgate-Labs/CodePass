---
phase: 05-quality-score-dashboard
plan: 01
subsystem: dashboard-scoring
tags: [dotnet, blazor-server, tdd, dashboard, quality-score, xunit]
requires:
  - phase: 03-rule-analysis-review
    provides: latest rule-analysis run DTOs with status, grouped severity counts, and failure messages
  - phase: 04-coverage-analysis-review
    provides: latest coverage-analysis run DTOs with line coverage totals, status, and failure messages
provides:
  - Tested current-snapshot quality score service for selected registered solutions
  - Immutable dashboard scoring DTOs for score, pass/fail status, rule contribution, coverage contribution, and blocking reasons
  - Scoped dependency-injection registration for IQualityScoreService
  - TDD regression coverage for pass, fail, and missing-or-failed evidence scoring rules
affects: [05-02, 05-03, 05-04, quality-dashboard-ui]
tech-stack:
  added: []
  patterns:
    - Current dashboard score is computed as a read model from latest persisted rule and coverage evidence, without storing score history
    - Missing, running, or failed evidence contributes zero points and emits readable blocking reasons
key-files:
  created:
    - src/CodePass.Web/Services/Dashboard/IQualityScoreService.cs
    - src/CodePass.Web/Services/Dashboard/QualityScoreModels.cs
    - src/CodePass.Web/Services/Dashboard/QualityScoreService.cs
    - tests/CodePass.Web.Tests/Services/QualityScoreServiceTests.cs
  modified:
    - src/CodePass.Web/Program.cs
key-decisions:
  - "Compute the dashboard quality score on demand from latest rule-analysis and coverage-analysis DTOs instead of persisting score rows."
  - "Represent missing/running/failed rule or coverage evidence as explicit contribution status plus blocking reasons so incomplete evidence cannot look like a passing snapshot."
  - "Expose separate rule and coverage contribution DTOs with max points, earned points, evidence status, counts, coverage totals, and summary text for upcoming dashboard UI components."
patterns-established:
  - "Dashboard backend services should consume existing result services instead of querying EF entities directly."
  - "Quality score status is Pass only when score is at least 80, both evidence sources succeeded, and the latest rule run has zero Error violations."
requirements-completed: [DASH-01, DASH-02, DASH-03]
duration: 3 min
completed: 2026-04-28
---

# Phase 05 Plan 01: Quality Score Current Snapshot Summary

**TDD-built dashboard scoring read model that combines latest rule-analysis and coverage evidence into a deterministic score, pass/fail status, and contribution breakdown.**

## Performance

- **Duration:** 3 min
- **Started:** 2026-04-28T13:28:14Z
- **Completed:** 2026-04-28T13:30:44Z
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments

- Added failing TDD coverage for the dashboard scoring contract before production types existed, covering perfect passing evidence, partial violation/coverage scoring, and missing-or-failed evidence blocking behavior.
- Implemented `IQualityScoreService` and immutable dashboard DTOs under `CodePass.Web.Services.Dashboard`.
- Implemented `QualityScoreService` to consume `IRuleAnalysisResultService.GetLatestRunForSolutionAsync` and `ICoverageAnalysisResultService.GetLatestRunForSolutionAsync`, calculate rule and coverage contributions, aggregate blocking reasons, and derive `Pass`/`Fail` status.
- Registered `IQualityScoreService` as scoped dependency injection in `Program.cs` for later dashboard UI work.

## Task Commits

Each task was committed atomically:

1. **Task 1: RED - specify current snapshot scoring behavior** - `5458de1` (test)
2. **Task 2: GREEN - implement score service and DI registration** - `7536236` (feat)

**Plan metadata:** Created in final docs commit.

_Note: This was a TDD plan with separate RED and GREEN commits; no REFACTOR commit was needed._

## Verification

- RED verification: `dotnet test CodePass.sln --filter "FullyQualifiedName~QualityScoreServiceTests"` failed because `CodePass.Web.Services.Dashboard`, `IQualityScoreService`, and the scoring models did not exist yet.
- GREEN verification: `dotnet test CodePass.sln --filter "FullyQualifiedName~QualityScoreServiceTests"` passed with 3 targeted tests.
- Final verification: `dotnet test CodePass.sln --filter "FullyQualifiedName~QualityScoreServiceTests" && dotnet build CodePass.sln` passed with 3 targeted tests and a clean solution build.

## Files Created/Modified

- `src/CodePass.Web/Services/Dashboard/IQualityScoreService.cs` - Defines `GetCurrentSnapshotAsync` for selected registered-solution dashboard scoring.
- `src/CodePass.Web/Services/Dashboard/QualityScoreModels.cs` - Defines immutable score snapshot, status, evidence status, rule contribution, and coverage contribution DTOs.
- `src/CodePass.Web/Services/Dashboard/QualityScoreService.cs` - Computes score, pass/fail status, evidence contributions, severity counts, coverage totals, summaries, and blocking reasons from latest result-service DTOs.
- `src/CodePass.Web/Program.cs` - Registers `IQualityScoreService` as a scoped service.
- `tests/CodePass.Web.Tests/Services/QualityScoreServiceTests.cs` - Covers TDD scoring behavior for perfect passing evidence, violation-driven failure, coverage contribution math, and missing/failed evidence blocking reasons.

## Decisions Made

- Compute quality score as an on-demand read model from latest rule and coverage run services, preserving the plan constraint not to add historical score tables.
- Use 50 maximum points for rule evidence and 50 maximum points for coverage evidence, with rule penalties and coverage percent conversion matching the plan's scoring formula.
- Model evidence state separately from overall score status so missing, running, and failed latest evidence are visible to the UI and always block a passing snapshot.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## Authentication Gates

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Plan `05-02` can consume `QualityScoreSnapshotDto`, `QualityRuleContributionDto`, and `QualityCoverageContributionDto` directly in reusable dashboard summary and evidence breakdown components.
- The backend contract already exposes the fields needed to render score, pass/fail state, severity counts, coverage totals, and readable blocking reasons for incomplete evidence.

## Self-Check: PASSED

- Verified all key created and modified files listed in this summary exist on disk.
- Verified task commits `5458de1` and `7536236` exist in git history.
- Verified final targeted scoring test and solution build command completed successfully.

---
*Phase: 05-quality-score-dashboard*
*Completed: 2026-04-28*
