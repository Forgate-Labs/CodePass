---
phase: 04-coverage-analysis-review
plan: 02
subsystem: coverage-analysis-persistence
tags: [dotnet, ef-core, sqlite, coverage-analysis, persistence, xunit]
requires:
  - phase: 01-registered-solutions
    provides: registered solution persistence used as the coverage-analysis run parent
provides:
  - SQLite-backed coverage-analysis run records parented by registered solutions
  - Project-level coverage summary rows for completed coverage runs
  - Class-level coverage rows with file paths and line/branch coverage percentages
  - Additive SQLite startup initialization for existing local databases
affects: [04-03, 04-04, 04-05, 04-06, quality-dashboard]
tech-stack:
  added: []
  patterns:
    - EF entities plus additive SQLite initializer remain migration-free for local self-hosted databases
    - Coverage percentages use double-backed REAL columns for SQLite-friendly storage and querying
key-files:
  created:
    - src/CodePass.Web/Data/Entities/CoverageAnalysisRun.cs
    - src/CodePass.Web/Data/Entities/CoverageAnalysisRunStatus.cs
    - src/CodePass.Web/Data/Entities/CoverageProjectSummary.cs
    - src/CodePass.Web/Data/Entities/CoverageClassCoverage.cs
  modified:
    - src/CodePass.Web/Data/CodePassDbContext.cs
    - src/CodePass.Web/Data/CodePassDatabaseInitializer.cs
    - tests/CodePass.Web.Tests/Services/CodePassDatabaseInitializerTests.cs
key-decisions:
  - "Use double-backed coverage percentages so SQLite stores coverage rates as REAL values and avoids decimal translation limitations."
  - "Add both separate and composite coverage-run lookup indexes so latest-run queries can filter by registered solution and order by start time efficiently."
patterns-established:
  - "Coverage persistence mirrors rule-analysis persistence: parent run metadata, child snapshot rows, cascade deletes, and additive SQLite table creation."
  - "Initializer regression tests cover both fresh SQLite databases and legacy pre-coverage databases with existing rule-analysis rows."
requirements-completed: [COV-02, COV-03, COV-04]
duration: 3 min
completed: 2026-04-27
---

# Phase 04 Plan 02: Coverage Persistence Schema Summary

**SQLite-backed coverage-analysis persistence with normalized run metadata, project summaries, class rows, EF mappings, and additive legacy database upgrades.**

## Performance

- **Duration:** 3 min
- **Started:** 2026-04-27T18:47:17Z
- **Completed:** 2026-04-27T18:51:04Z
- **Tasks:** 3
- **Files modified:** 7

## Accomplishments

- Added coverage-analysis run, status, project-summary, and class-coverage entities with line and branch totals, percentages, timestamps, error messages, and child collections.
- Mapped EF Core relationships, string status conversion, required registered-solution ownership, run-to-child cascade deletes, and lookup indexes for latest run, project, and class queries.
- Extended SQLite startup initialization with idempotent coverage tables and indexes so existing local `codepass.db` files can upgrade in place without migrations or deletion.
- Expanded initializer tests to prove fresh database creation, legacy pre-coverage database upgrades, critical index creation, and preservation of existing registered solution, authored rule, assignment, and rule-analysis rows.

## Task Commits

Each task was committed atomically:

1. **Task 1: Add coverage persistence entities and EF mappings** - `fb917c3` (feat)
2. **Task 2: Extend additive SQLite initialization for coverage tables** - `3b2c03f` (feat)
3. **Task 3: Cover coverage schema creation and legacy SQLite upgrades** - `930240d` (test)

**Plan metadata:** final docs commit (see git history for `docs(04-02): complete coverage persistence schema plan`)

## Verification

- `dotnet build CodePass.sln` passed after Task 1.
- `dotnet build CodePass.sln` passed after Task 2.
- `dotnet test CodePass.sln --filter "FullyQualifiedName~CodePassDatabaseInitializerTests"` passed after Task 3 with 3 initializer tests.
- Final verification passed: `dotnet test CodePass.sln --filter "FullyQualifiedName~CodePassDatabaseInitializerTests" && dotnet build CodePass.sln`.

## Files Created/Modified

- `src/CodePass.Web/Data/Entities/CoverageAnalysisRun.cs` - Stores coverage run metadata for one registered solution, including status, timestamps, project/class counts, aggregate line/branch coverage totals and percentages, optional error message, and child rows.
- `src/CodePass.Web/Data/Entities/CoverageAnalysisRunStatus.cs` - Defines the running, succeeded, and failed lifecycle states for persisted coverage runs.
- `src/CodePass.Web/Data/Entities/CoverageProjectSummary.cs` - Stores project-level coverage snapshots for a coverage run.
- `src/CodePass.Web/Data/Entities/CoverageClassCoverage.cs` - Stores class-level coverage snapshots with project name, class name, file path, and line/branch coverage values.
- `src/CodePass.Web/Data/CodePassDbContext.cs` - Adds coverage DbSets, relationships, cascade behavior, string enum conversion, and coverage lookup indexes.
- `src/CodePass.Web/Data/CodePassDatabaseInitializer.cs` - Adds idempotent SQLite `CREATE TABLE IF NOT EXISTS` and `CREATE INDEX IF NOT EXISTS` blocks for coverage runs, project summaries, and class rows.
- `tests/CodePass.Web.Tests/Services/CodePassDatabaseInitializerTests.cs` - Covers fresh SQLite coverage schema creation, critical coverage indexes, legacy pre-coverage upgrades, and existing row preservation.

## Decisions Made

- Used `double` for persisted coverage percentages so SQLite stores them as `REAL` values and avoids the provider translation limitations that affected earlier `DateTimeOffset`/ordering work.
- Added a composite `RegisteredSolutionId` + `StartedAtUtc` coverage-run index in addition to the separately requested indexes, because latest coverage run retrieval will need both filter and ordering fields together.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## Authentication Gates

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Plan `04-03` can persist normalized coverage engine outputs into `CoverageAnalysisRun`, `CoverageProjectSummary`, and `CoverageClassCoverage` rows and retrieve the latest run for the selected solution.
- Later coverage UI and dashboard work can rely on normalized SQLite rows with registered-solution ownership and cascade cleanup behavior.

## Self-Check: PASSED

- Verified all key created and modified files listed in this summary exist on disk.
- Verified task commits `fb917c3`, `3b2c03f`, and `930240d` exist in git history.
- Verified final initializer test and build commands completed successfully.

---
*Phase: 04-coverage-analysis-review*
*Completed: 2026-04-27*
