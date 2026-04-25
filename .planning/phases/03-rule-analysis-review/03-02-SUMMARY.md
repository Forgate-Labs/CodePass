---
phase: 03-rule-analysis-review
plan: 02
subsystem: analysis
tags: [dotnet, roslyn, msbuildworkspace, rules, xunit]
requires:
  - phase: 02-user-authored-rule-definitions
    provides: AuthoredRuleDefinitionDto DSL metadata, severity, scope JSON, and parameter JSON for user-authored rules
  - phase: 03-rule-analysis-review
    provides: Per-solution rule selection services that can supply selected authored rules to the analyzer
provides:
  - Roslyn-backed analyzer abstraction for executing selected authored rule DSL documents against local .sln paths
  - Rule-analysis findings carrying rule identity, severity, relative file path, message, and 1-based line/column spans
  - Regression coverage for syntax_presence, forbidden_api_usage, symbol_naming, scoping, invalid JSON, and cancellation behavior
affects: [03-03, 03-04, 03-05, rule-analysis-runs, rule-analysis-ui]
tech-stack:
  added: [Microsoft.CodeAnalysis.CSharp.Workspaces, Microsoft.CodeAnalysis.Workspaces.MSBuild, Microsoft.Build.Locator]
  patterns: [Guarded MSBuildLocator registration, authored-rule-only analyzer inputs, Roslyn syntax/semantic traversal without source-text string matching]
key-files:
  created:
    - src/CodePass.Web/Services/RuleAnalysis/IRuleAnalyzer.cs
    - src/CodePass.Web/Services/RuleAnalysis/RuleAnalysisEngineModels.cs
    - src/CodePass.Web/Services/RuleAnalysis/RoslynRuleAnalyzer.cs
    - tests/CodePass.Web.Tests/Services/RoslynRuleAnalyzerTests.cs
  modified:
    - src/CodePass.Web/CodePass.Web.csproj
key-decisions:
  - "Execute only the AuthoredRuleDefinitionDto instances passed to the analyzer; unknown rule kinds are skipped so catalog evolution does not activate built-in rules or crash existing runs."
  - "Use MSBuildWorkspace with guarded MSBuildLocator.RegisterDefaults() for local solution loading, while keeping MSBuild framework runtime assets excluded to avoid assembly-loading conflicts."
  - "Return analyzer locations as solution-relative paths plus 1-based line and column spans from Roslyn Location data so persisted results and UI can point at actionable code positions."
patterns-established:
  - "Rule analyzers parse authored scope/parameter JSON at execution boundaries and surface invalid rule JSON as rule-code-specific InvalidOperationException errors."
  - "Scope matching honors files and excludeFiles with a small deterministic glob subset before any syntax or semantic rule execution."
requirements-completed: [RULE-01, RULE-06]
duration: 5 min
completed: 2026-04-25
---

# Phase 3 Plan 2: Roslyn-backed Authored Rule Analyzer Summary

**Roslyn MSBuildWorkspace analyzer for selected user-authored DSL rules with severity, relative file paths, messages, and precise source locations.**

## Performance

- **Duration:** 5 min
- **Started:** 2026-04-25T03:46:18Z
- **Completed:** 2026-04-25T03:51:39Z
- **Tasks:** 3
- **Files modified:** 5

## Accomplishments
- Added `IRuleAnalyzer` and `RuleAnalysisFinding` as the execution contract and result shape for later run orchestration.
- Implemented `RoslynRuleAnalyzer` with guarded MSBuild registration, solution-relative file paths, scoped rule execution, syntax policies, semantic API matching, and symbol naming checks.
- Added xUnit regression coverage that builds temporary C# solutions and proves authored severity, file path, non-zero code locations, exclusion scopes, invalid JSON errors, and cancellation behavior.

## Task Commits

Each task was committed atomically:

1. **Task 1: RED - add analyzer contract and failing rule-engine tests** - `659c3f6` (test)
2. **Task 2: GREEN - implement Roslyn DSL execution** - `59bcdc9` (feat)
3. **Task 3: REFACTOR - harden analyzer edge cases without changing behavior** - `d464c04` (refactor)

**Plan metadata:** final docs commit (see git history for `docs(03-02): complete Roslyn rule analyzer plan`)

_Note: This TDD plan produced separate RED, GREEN, and REFACTOR commits._

## Files Created/Modified
- `src/CodePass.Web/CodePass.Web.csproj` - Added Roslyn Workspaces/MSBuild package references plus MSBuild runtime exclusion needed by MSBuildLocator.
- `src/CodePass.Web/Services/RuleAnalysis/IRuleAnalyzer.cs` - Defines the analyzer abstraction used by future run orchestration.
- `src/CodePass.Web/Services/RuleAnalysis/RuleAnalysisEngineModels.cs` - Defines `RuleAnalysisFinding` with rule identity, severity, message, relative path, and start/end locations.
- `src/CodePass.Web/Services/RuleAnalysis/RoslynRuleAnalyzer.cs` - Executes authored `syntax_presence`, `forbidden_api_usage`, and `symbol_naming` rules through Roslyn.
- `tests/CodePass.Web.Tests/Services/RoslynRuleAnalyzerTests.cs` - Covers DSL-to-finding behavior with temporary local C# solutions.

## Decisions Made
- Execute only selected authored rule DTOs supplied to the analyzer; supported catalog kinds are interpreter capabilities, not active built-in rules.
- Skip unknown rule kinds rather than throwing, preserving compatibility as the closed catalog evolves.
- Compare forbidden API usage with Roslyn semantic symbols and inspect syntax constructs with Roslyn syntax nodes, avoiding source-text string matching.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Excluded MSBuild framework runtime assets for MSBuildLocator compatibility**
- **Found during:** Task 1 (RED - add analyzer contract and failing rule-engine tests)
- **Issue:** Adding `Microsoft.CodeAnalysis.Workspaces.MSBuild` introduced `Microsoft.Build.Framework` runtime assets, and `Microsoft.Build.Locator` failed the test build with MSBL001 because MSBuild assemblies must not be copied locally.
- **Fix:** Added an explicit `Microsoft.Build.Framework` package reference with `ExcludeAssets="runtime"` and `PrivateAssets="all"` so MSBuildLocator can register the SDK MSBuild assemblies safely.
- **Files modified:** `src/CodePass.Web/CodePass.Web.csproj`
- **Verification:** `dotnet test CodePass.sln --filter "FullyQualifiedName~RoslynRuleAnalyzerTests"` compiled and failed only for expected RED behavior, then passed after GREEN/REFACTOR.
- **Committed in:** `659c3f6` (part of Task 1 commit)

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** The fix was necessary for Roslyn/MSBuildWorkspace package compatibility and did not change product scope.

## Issues Encountered
None beyond the auto-fixed MSBuildLocator package compatibility blocker documented above.

## Authentication Gates
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Plan 03-03 can persist `RuleAnalysisFinding` outputs with stable rule identity, severity, file path, message, and source span fields.
- Plan 03-04 can call `IRuleAnalyzer` with per-solution selected authored rules and the registered local `.sln` path.
- No active built-in rule pack was introduced; the analyzer remains authored-rule-only.

---
*Phase: 03-rule-analysis-review*
*Completed: 2026-04-25*

## Self-Check: PASSED
- Verified created files exist: `IRuleAnalyzer.cs`, `RuleAnalysisEngineModels.cs`, `RoslynRuleAnalyzer.cs`, `RoslynRuleAnalyzerTests.cs`, and this summary.
- Verified task commits exist: `659c3f6`, `59bcdc9`, and `d464c04`.
