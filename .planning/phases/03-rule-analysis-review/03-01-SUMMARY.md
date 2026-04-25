---
phase: 03-rule-analysis-review
plan: 01
subsystem: rule-analysis
tags: [dotnet, ef-core, sqlite, rules, xunit, per-solution-selection]
requires:
  - phase: 01-registered-solutions
    provides: registered solution persistence used as the assignment parent
  - phase: 02-user-authored-rule-definitions
    provides: authored-rule persistence and authored-only active-rule constraints
provides:
  - Persisted solution-to-authored-rule assignment rows with per-solution enabled state
  - Selection service for listing, toggling, and resolving enabled authored rules per registered solution
  - SQLite startup upgrade support for legacy local databases missing assignment tables
affects: [03-02, 03-03, 03-04, 03-05, rule-analysis]
tech-stack:
  added: []
  patterns: [explicit per-solution assignment join, dual global/per-solution enabled checks, additive SQLite table initialization]
key-files:
  created:
    - src/CodePass.Web/Data/Entities/SolutionRuleAssignment.cs
    - src/CodePass.Web/Services/RuleAnalysis/ISolutionRuleSelectionService.cs
    - src/CodePass.Web/Services/RuleAnalysis/SolutionRuleSelectionModels.cs
    - src/CodePass.Web/Services/RuleAnalysis/SolutionRuleSelectionService.cs
    - tests/CodePass.Web.Tests/Services/SolutionRuleSelectionServiceTests.cs
  modified:
    - src/CodePass.Web/Data/CodePassDbContext.cs
    - src/CodePass.Web/Data/CodePassDatabaseInitializer.cs
    - src/CodePass.Web/Program.cs
    - src/CodePass.Web/CodePass.Web.csproj
    - tests/CodePass.Web.Tests/Services/CodePassDatabaseInitializerTests.cs
key-decisions:
  - "Persist per-solution rule applicability as explicit SolutionRuleAssignment rows instead of inferring active rules from global authored-rule state."
  - "Require both the authored rule's global IsEnabled flag and the per-solution assignment IsEnabled flag before a rule is returned for analysis."
  - "Keep SQLite startup initialization additive and idempotent so existing local databases gain assignment support without manual deletion."
patterns-established:
  - "Rule-analysis services live under CodePass.Web.Services.RuleAnalysis and consume authored-rule entities without querying catalog metadata as active rules."
  - "Selection rows list every persisted authored rule for a solution, defaulting to disabled until a SolutionRuleAssignment exists."
requirements-completed: [RULE-04]
duration: 3 min
completed: 2026-04-25
---

# Phase 03 Plan 01: Per-solution authored-rule assignment Summary

**SQLite-backed per-solution authored-rule selection with explicit assignment rows and enabled-rule resolution for rule analysis.**

## Performance

- **Duration:** 3 min
- **Started:** 2026-04-25T03:46:21Z
- **Completed:** 2026-04-25T03:49:38Z
- **Tasks:** 3
- **Files modified:** 10

## Accomplishments
- Added `SolutionRuleAssignment` persistence with required registered-solution/authored-rule foreign keys, cascade deletes, and a unique composite index.
- Implemented `ISolutionRuleSelectionService` so callers can list authored-rule selections for one solution, toggle one rule, and retrieve only rules enabled both globally and for that solution.
- Extended SQLite startup initialization and regression tests so legacy local databases gain the assignment table without losing registered solution or authored-rule data.

## Task Commits

Each task was committed atomically:

1. **Task 1: Add solution-rule assignment persistence** - `19edfec` (feat)
2. **Task 2: Implement the per-solution rule selection service** - `6f8e60a` (feat)
3. **Task 3: Cover assignment behavior and SQLite upgrade** - `76c3b7d` (test)

## Verification

- `dotnet build CodePass.sln` passed after Task 1.
- `dotnet build CodePass.sln` passed after Task 2.
- `dotnet test CodePass.sln --filter "FullyQualifiedName~SolutionRuleSelectionServiceTests|FullyQualifiedName~CodePassDatabaseInitializerTests"` passed after Task 3.
- Final verification passed: `dotnet test CodePass.sln --filter "FullyQualifiedName~SolutionRuleSelectionServiceTests|FullyQualifiedName~CodePassDatabaseInitializerTests" && dotnet build CodePass.sln`.

## Files Created/Modified
- `src/CodePass.Web/Data/Entities/SolutionRuleAssignment.cs` - Persisted join row between registered solutions and user-authored rules with per-solution enabled state and timestamps.
- `src/CodePass.Web/Data/CodePassDbContext.cs` - Adds `SolutionRuleAssignments`, required FK mappings, cascade delete behavior, and unique `(RegisteredSolutionId, AuthoredRuleDefinitionId)` index.
- `src/CodePass.Web/Data/CodePassDatabaseInitializer.cs` - Adds idempotent creation of the assignment table and index while preserving the authored-rule table upgrade path.
- `src/CodePass.Web/Services/RuleAnalysis/ISolutionRuleSelectionService.cs` - Service contract for listing, toggling, and resolving enabled rules per solution.
- `src/CodePass.Web/Services/RuleAnalysis/SolutionRuleSelectionModels.cs` - Selection DTO and toggle request model for rule-analysis selection flows.
- `src/CodePass.Web/Services/RuleAnalysis/SolutionRuleSelectionService.cs` - Implements authored-only selection reads, validated assignment upserts, and enabled-rule resolution.
- `src/CodePass.Web/Program.cs` - Registers the solution rule selection service with scoped dependency injection.
- `src/CodePass.Web/CodePass.Web.csproj` - Adds safe MSBuild framework package metadata needed for clean builds with existing Roslyn/MSBuild package references.
- `tests/CodePass.Web.Tests/Services/SolutionRuleSelectionServiceTests.cs` - Covers default disabled selections, per-solution isolation, update-not-duplicate behavior, global/per-solution enabled filtering, and clear unknown-id exceptions.
- `tests/CodePass.Web.Tests/Services/CodePassDatabaseInitializerTests.cs` - Extends SQLite upgrade coverage to include assignment table creation and preservation of existing registered solution/authored-rule data.

## Decisions Made
- Persisted applicability in a dedicated join table instead of overloading authored-rule global enabled state.
- Treated the rule catalog as editor metadata only; the new service queries only persisted `AuthoredRuleDefinitions` and `SolutionRuleAssignments`.
- Returned enabled analysis rules only when both the authored rule and the solution-specific assignment are enabled.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Added safe Microsoft.Build.Framework package metadata for clean builds**
- **Found during:** Task 2 (Implement the per-solution rule selection service)
- **Issue:** `dotnet build CodePass.sln` began failing with `MSBL001` because the existing Roslyn/MSBuild package graph included `Microsoft.Build.Framework` without the metadata required by `Microsoft.Build.Locator`.
- **Fix:** Added an explicit `Microsoft.Build.Framework` package reference with `ExcludeAssets="runtime"` and `PrivateAssets="all"` so the solution builds without MSBuild assembly-loading warnings promoted to errors.
- **Files modified:** `src/CodePass.Web/CodePass.Web.csproj`
- **Verification:** `dotnet build CodePass.sln`
- **Committed in:** `6f8e60a` (part of Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** The fix was required to complete planned build verification and did not change product scope.

## Issues Encountered
- Build verification exposed the MSBuild package metadata issue documented above; it was fixed and verified during Task 2.

## Authentication Gates

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Phase 3 can now analyze solution-specific authored-rule selections without accidentally promoting catalog metadata or globally disabled rules into active analysis.
- Plan `03-02` can use `GetEnabledRuleDefinitionsForSolutionAsync` as the authored-only source for Roslyn rule execution.

## Self-Check: PASSED

- Verified all key implementation/test files listed in this summary exist on disk.
- Verified task commits `19edfec`, `6f8e60a`, and `76c3b7d` exist in git history.

---
*Phase: 03-rule-analysis-review*
*Completed: 2026-04-25*
