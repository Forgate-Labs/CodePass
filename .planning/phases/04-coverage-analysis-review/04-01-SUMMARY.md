---
phase: 04-coverage-analysis-review
plan: 01
subsystem: coverage-analysis
tags: [dotnet, cobertura, xunit, coverage, parser]
requires:
  - phase: 01-registered-solutions
    provides: Registered local .sln paths that coverage analysis can execute against
provides:
  - Coverage analyzer contract for running backend coverage analysis against a solution path
  - Immutable normalized coverage DTOs for project summaries and class coverage rows
  - Cobertura XML parser that aggregates generated coverage files into deterministic normalized results
  - dotnet test XPlat Code Coverage runner with readable failure messages and generated Cobertura discovery
affects: [04-02, 04-03, 04-04, 04-05, coverage-analysis-runs, coverage-analysis-ui, quality-score-dashboard]
tech-stack:
  added: []
  patterns: [System.Xml.Linq Cobertura normalization, isolated process runner, immutable engine DTOs separate from persistence entities]
key-files:
  created:
    - src/CodePass.Web/Services/CoverageAnalysis/ICoverageAnalyzer.cs
    - src/CodePass.Web/Services/CoverageAnalysis/CoverageAnalysisEngineModels.cs
    - src/CodePass.Web/Services/CoverageAnalysis/CoberturaCoverageParser.cs
    - src/CodePass.Web/Services/CoverageAnalysis/DotNetCoverageAnalyzer.cs
    - tests/CodePass.Web.Tests/Services/CoberturaCoverageParserTests.cs
  modified: []
key-decisions:
  - "Keep coverage normalization isolated in CoberturaCoverageParser so persistence and UI work can consume normalized DTOs without shelling out to dotnet."
  - "Keep DotNetCoverageAnalyzer responsible only for process execution, generated coverage-file discovery, readable failures, and temp-directory cleanup."
  - "Represent coverage engine outputs as immutable records that are not tied to EF persistence entities."
patterns-established:
  - "Coverage percentages are derived from covered/total counts and rounded to two decimals with zero totals returning 0."
  - "Coverage class rows aggregate by project name, class name, and file path, then sort ordinally by the same keys for stable UI/persistence inputs."
requirements-completed: [COV-01, COV-02, COV-03, COV-04]
duration: 3 min
completed: 2026-04-27
---

# Phase 4 Plan 1: Coverage Analysis Engine Summary

**Cobertura coverage normalization and dotnet test coverage execution with immutable project and class coverage results.**

## Performance

- **Duration:** 3 min
- **Started:** 2026-04-27T18:47:31Z
- **Completed:** 2026-04-27T18:51:21Z
- **Tasks:** 3
- **Files modified:** 5

## Accomplishments
- Added `ICoverageAnalyzer` as the backend contract for analyzing a local `.sln` path and returning normalized coverage results.
- Added immutable coverage engine records for project summaries and class rows with covered/total line and branch counts plus deterministic percentages.
- Implemented `CoberturaCoverageParser` using `System.Xml.Linq` to parse one or more Cobertura XML files, aggregate duplicate class rows, compute line/branch totals, and produce stable ordering.
- Implemented `DotNetCoverageAnalyzer` to run `dotnet test` with the XPlat Code Coverage collector, capture readable output on failures, discover generated `coverage.cobertura.xml` files, and clean temporary results.
- Added xUnit regression coverage for normal Cobertura documents, multi-file aggregation, empty packages, missing filenames, absent branch data, and stable ordering.

## Task Commits

Each task was committed atomically:

1. **Task 1: RED - define coverage normalization behavior with failing parser tests** - `43320a4` (test)
2. **Task 2: GREEN - implement Cobertura normalization and dotnet coverage execution** - `c2c4120` (feat)
3. **Task 3: REFACTOR - harden normalization edge cases without changing behavior** - `66727e1` (refactor)

**Plan metadata:** final docs commit (see git history for `docs(04-01): complete coverage analysis engine plan`)

_Note: This TDD plan produced separate RED, GREEN, and REFACTOR commits._

## Files Created/Modified
- `src/CodePass.Web/Services/CoverageAnalysis/ICoverageAnalyzer.cs` - Defines the async coverage analyzer contract for solution paths.
- `src/CodePass.Web/Services/CoverageAnalysis/CoverageAnalysisEngineModels.cs` - Defines normalized immutable coverage result, project summary, and class coverage records.
- `src/CodePass.Web/Services/CoverageAnalysis/CoberturaCoverageParser.cs` - Parses Cobertura XML with line/branch aggregation, percentage calculation, duplicate row merging, and deterministic ordering.
- `src/CodePass.Web/Services/CoverageAnalysis/DotNetCoverageAnalyzer.cs` - Runs `dotnet test` with XPlat Code Coverage, handles readable errors, locates generated Cobertura files, parses them, and cleans temp output.
- `tests/CodePass.Web.Tests/Services/CoberturaCoverageParserTests.cs` - Covers RED/GREEN/REFACTOR parser behavior and edge cases.

## Decisions Made
- Keep XML coverage normalization separate from process execution so persistence and UI tests can use normalized DTOs without invoking `dotnet test`.
- Use immutable engine records instead of EF entities for coverage analyzer outputs, preserving a clean boundary for later persistence plans.
- Derive percentages from raw covered/total counts with two-decimal rounding and define zero-total percentages as `0`.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
- During Task 3, unrelated in-progress coverage persistence/database-initializer work temporarily made the in-place `dotnet build CodePass.sln` fail outside Plan 04-01. Plan 04-01 was first verified in a clean detached worktree at commit `66727e1`, and the final checkout now also passes `dotnet test CodePass.sln --filter "FullyQualifiedName~CoberturaCoverageParserTests" && dotnet build CodePass.sln`. Details are logged in `deferred-items.md`.

## Authentication Gates

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Plan 04-02 can add persistence schema around the normalized project summary and class coverage record shapes.
- Plan 04-03 can persist `CoverageAnalysisResult` outputs without coupling parser tests to database entities.
- Plan 04-04 can orchestrate `ICoverageAnalyzer` from registered solution paths and surface readable coverage execution failures.

---
*Phase: 04-coverage-analysis-review*
*Completed: 2026-04-27*

## Self-Check: PASSED
- Verified created files exist: `ICoverageAnalyzer.cs`, `CoverageAnalysisEngineModels.cs`, `CoberturaCoverageParser.cs`, `DotNetCoverageAnalyzer.cs`, `CoberturaCoverageParserTests.cs`, this summary, and `deferred-items.md`.
- Verified task commits exist: `43320a4`, `c2c4120`, and `66727e1`.
- Verified clean worktree plan checks pass at `66727e1`, and verified final checkout checks pass after metadata commit preparation: `dotnet test CodePass.sln --filter "FullyQualifiedName~CoberturaCoverageParserTests" && dotnet build CodePass.sln`.
