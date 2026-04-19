# CodePass Requirements

## v1 Requirements

### Projects

- [x] **PROJ-01**: User can register a codebase by providing the local path to a `.sln` file.
- [x] **PROJ-02**: User can validate that the supplied path exists and points to a real `.sln` before saving it.
- [x] **PROJ-03**: User can view the current registration details and status for each registered solution.
- [x] **PROJ-04**: User can update or remove a registered solution when the target path changes or is no longer needed.

### Rules

- [ ] **RULE-01**: User can manually start a rule-analysis run for a registered solution.
- [ ] **RULE-02**: User can create a custom rule from scratch through a schema-driven editor backed by the rule DSL.
- [ ] **RULE-03**: User can edit a custom rule as raw JSON when they need direct control over the DSL.
- [ ] **RULE-04**: User can enable or disable a custom rule per registered solution.
- [ ] **RULE-05**: User can review rule-analysis results grouped by rule.
- [ ] **RULE-06**: User can inspect each rule violation with its severity, file path, and code location.
- [ ] **RULE-07**: User can work only with user-authored rules in the product, without depending on built-in production rule packs.

### Coverage

- [ ] **COV-01**: User can manually start a coverage-analysis run for a registered solution.
- [ ] **COV-02**: User can view unit test coverage for each class in the analyzed solution.
- [ ] **COV-03**: User can view a project-level coverage summary for the current analyzed solution.
- [ ] **COV-04**: User can view normalized coverage results in the UI after a coverage run completes.

### Dashboard

- [ ] **DASH-01**: User can see a project score for the current analysis snapshot.
- [ ] **DASH-02**: User can see whether the current snapshot passes or fails.
- [ ] **DASH-03**: User can understand how rule results and coverage results contribute to the current score.

## v2 Requirements

- [ ] **RULE-08**: User can baseline existing issues so new work is easier to review on legacy codebases.
- [ ] **RULE-09**: User can import or align external Roslyn-analyzer results with CodePass reporting.
- [ ] **AUTO-01**: User can trigger analyses from CI or pipeline integrations.
- [ ] **DASH-04**: User can review historical runs and quality trends over time.

## Out of Scope

- Built-in default production rules or rule packs — v1 should validate user-authored rules first; repository examples are enough for learning.
- GitHub, Azure DevOps, pull-request, and branch integrations — defer until the local manual workflow proves valuable.
- Automatic scheduling or pipeline-triggered execution — manual runs are enough for v1 validation.
- Multi-language support beyond C#/.NET — focus is part of the product strategy.
- Enterprise auth, multi-tenant workspaces, and advanced permission models — unnecessary complexity for the first version.
- Trend-heavy dashboards and historical analytics — current-state clarity matters more than history in v1.

## Traceability

| REQ-ID | Roadmap Phase | Status |
|--------|---------------|--------|
| PROJ-01 | Phase 1 - Registered Solutions | Complete |
| PROJ-02 | Phase 1 - Registered Solutions | Complete |
| PROJ-03 | Phase 1 - Registered Solutions | Complete |
| PROJ-04 | Phase 1 - Registered Solutions | Complete |
| RULE-02 | Phase 2 - User-Authored Rule Definitions | Pending |
| RULE-03 | Phase 2 - User-Authored Rule Definitions | Pending |
| RULE-07 | Phase 2 - User-Authored Rule Definitions | Pending |
| RULE-01 | Phase 3 - Rule Analysis Review | Pending |
| RULE-04 | Phase 3 - Rule Analysis Review | Pending |
| RULE-05 | Phase 3 - Rule Analysis Review | Pending |
| RULE-06 | Phase 3 - Rule Analysis Review | Pending |
| COV-01 | Phase 4 - Coverage Analysis Review | Pending |
| COV-02 | Phase 4 - Coverage Analysis Review | Pending |
| COV-03 | Phase 4 - Coverage Analysis Review | Pending |
| COV-04 | Phase 4 - Coverage Analysis Review | Pending |
| DASH-01 | Phase 5 - Quality Score Dashboard | Pending |
| DASH-02 | Phase 5 - Quality Score Dashboard | Pending |
| DASH-03 | Phase 5 - Quality Score Dashboard | Pending |

---
*Last updated: 2026-04-19 after roadmap generation*
