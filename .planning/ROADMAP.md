# Roadmap: CodePass

## Overview

CodePass v1 moves from trusted local solution registration, to user-authored rule definition, to actionable rule and coverage analysis, and finally to a score dashboard that makes pass/fail obvious. The roadmap stays tightly scoped to self-hosted .NET workflows and explicitly keeps built-in production rule packs out of the product so the v1 experience validates user-authored rules first.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

- [x] **Phase 1: Registered Solutions** - Users can register, inspect, and maintain the local `.sln` targets CodePass analyzes.
- [x] **Phase 2: User-Authored Rule Definitions** - Admin users can create and edit only user-authored rules through the DSL-backed rule editor.
- [x] **Phase 3: Rule Analysis Review** - Users can apply authored rules to solutions, run analysis manually, and inspect actionable findings.
- [x] **Phase 4: Coverage Analysis Review** - Users can run coverage analysis manually and inspect normalized coverage results.
- [ ] **Phase 5: Quality Score Dashboard** - Users can understand the current pass/fail state and score from rule and coverage evidence.

## Phase Details

### Phase 1: Registered Solutions
**Goal**: Users can register and manage the .NET solutions that CodePass will analyze.
**Depends on**: Nothing (first phase)
**Requirements**: PROJ-01, PROJ-02, PROJ-03, PROJ-04
**Success Criteria** (what must be TRUE):
  1. User can add a solution by entering a local `.sln` path, and the product rejects invalid or non-solution paths before saving.
  2. User can see each registered solution with its saved registration details and current status.
  3. User can update a registered solution when its path changes or remove it when it is no longer needed.
**Plans**: 3 plans
Plans:
- [x] 01-01-PLAN.md — Scaffold the Blazor Server + SQLite registration foundation, validation rules, and status-refresh backend.
- [x] 01-02-PLAN.md — Build the registered-solutions page with card layout, add modal, and picker-assisted create flow.
- [x] 01-03-PLAN.md — Add the edit/remove modal workflow and final full-flow verification.

### Phase 2: User-Authored Rule Definitions
**Goal**: Admin users can define and maintain custom analysis rules without relying on shipped production rule packs.
**Depends on**: Phase 1
**Requirements**: RULE-02, RULE-03, RULE-07
**Success Criteria** (what must be TRUE):
  1. Admin user can create a new custom rule from scratch through a schema-driven editor backed by the rule DSL.
  2. Admin user can switch to raw JSON editing when direct control over the DSL is needed.
  3. The product exposes only user-authored rules as active rules inside CodePass, with no built-in production rule pack required or enabled.
**Plans**: 3 plans
Plans:
- [x] 02-01-PLAN.md — Add authored-rule persistence, the closed rule-kind catalog, and authored-only active-rule services.
- [x] 02-02-PLAN.md — Build the `/rules` page, authored-rule listing UI, and schema-driven editor flow.
- [x] 02-03-PLAN.md — Add raw JSON editing, JSON validation, and final `/rules` workflow verification.

### Phase 3: Rule Analysis Review
**Goal**: Users can decide which authored rules apply to each solution, run rule analysis on demand, and inspect violations clearly.
**Depends on**: Phase 1, Phase 2
**Requirements**: RULE-01, RULE-04, RULE-05, RULE-06
**Success Criteria** (what must be TRUE):
  1. User can enable or disable authored rules for a specific registered solution.
  2. User can manually start a rule-analysis run for a registered solution.
  3. After a run completes, user can review rule-analysis results grouped by rule.
  4. User can inspect each reported violation with its severity, file path, and code location.
**Plans**: 6 plans
Plans:
- [x] 03-01-PLAN.md — Add per-solution authored-rule assignment persistence and selection services.
- [x] 03-02-PLAN.md — Build the Roslyn-backed authored-rule analyzer with TDD coverage.
- [x] 03-03-PLAN.md — Persist rule-analysis runs and grouped violation results.
- [x] 03-04-PLAN.md — Wire manual run orchestration from selected rules to persisted results.
- [x] 03-05-PLAN.md — Build the `/analysis/rules` UI for rule selection, manual runs, and grouped results.
- [x] 03-06-PLAN.md — Verify the completed rule-analysis workflow in the running app.

### Phase 4: Coverage Analysis Review
**Goal**: Users can run coverage analysis for a registered solution and inspect the normalized coverage outputs CodePass uses.
**Depends on**: Phase 1
**Requirements**: COV-01, COV-02, COV-03, COV-04
**Success Criteria** (what must be TRUE):
  1. User can manually start a coverage-analysis run for a registered solution.
  2. After the run completes, user can view normalized coverage results in the UI for the current analyzed solution.
  3. User can inspect unit test coverage for each class in the analyzed solution.
  4. User can inspect a project-level coverage summary for the current analyzed solution.
**Plans**: 6 plans
Plans:
- [x] 04-01-PLAN.md — Build the TDD coverage engine, Cobertura normalization, and dotnet test runner.
- [x] 04-02-PLAN.md — Add coverage run, project summary, and class coverage persistence schema.
- [x] 04-03-PLAN.md — Persist normalized coverage results and expose latest-run DTO retrieval.
- [x] 04-04-PLAN.md — Wire manual coverage run orchestration from registered solutions to persisted results.
- [x] 04-05-PLAN.md — Build the `/analysis/coverage` UI for manual runs, project summaries, and class rows.
- [x] 04-06-PLAN.md — Verify the completed coverage-analysis workflow in the running app.

### Phase 5: Quality Score Dashboard
**Goal**: Users can understand whether the current analysis snapshot passes and how rule and coverage evidence drive the score.
**Depends on**: Phase 3, Phase 4
**Requirements**: DASH-01, DASH-02, DASH-03
**Success Criteria** (what must be TRUE):
  1. User can see a project score for the current analysis snapshot.
  2. User can immediately tell whether the current snapshot passes or fails.
  3. User can understand how rule results and coverage results contribute to the current score.
**Plans**: 4 plans
Plans:
- [x] 05-01-PLAN.md — Build the TDD current-snapshot scoring service from latest rule and coverage evidence.
- [ ] 05-02-PLAN.md — Create reusable dashboard score summary and evidence breakdown components.
- [ ] 05-03-PLAN.md — Build the `/dashboard` page and sidebar navigation workflow.
- [ ] 05-04-PLAN.md — Verify the completed quality dashboard with automated and running-app checks.

## Requirement Coverage

- PROJ-01 → Phase 1
- PROJ-02 → Phase 1
- PROJ-03 → Phase 1
- PROJ-04 → Phase 1
- RULE-02 → Phase 2
- RULE-03 → Phase 2
- RULE-07 → Phase 2
- RULE-01 → Phase 3
- RULE-04 → Phase 3
- RULE-05 → Phase 3
- RULE-06 → Phase 3
- COV-01 → Phase 4
- COV-02 → Phase 4
- COV-03 → Phase 4
- COV-04 → Phase 4
- DASH-01 → Phase 5
- DASH-02 → Phase 5
- DASH-03 → Phase 5

**Coverage status**: 18/18 v1 requirements mapped. No orphaned requirements.

## Progress

**Execution Order:**
Phases execute in numeric order: 1 → 2 → 3 → 4 → 5

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Registered Solutions | 3/3 | Complete | 2026-04-19 |
| 2. User-Authored Rule Definitions | 3/3 | Complete | 2026-04-25 |
| 3. Rule Analysis Review | 6/6 | Complete | 2026-04-25 |
| 4. Coverage Analysis Review | 6/6 | Complete | 2026-04-28 |
| 5. Quality Score Dashboard | 1/4 | In Progress | - |
