# CodePass

CodePass is a self-hosted code quality platform for C#/.NET solutions. It focuses on local, lightweight, deeply .NET-native analysis with customizable JSON rules executed on top of Roslyn.

The product is inspired by quality platforms such as SonarQube, but with a narrower scope: C#/.NET, local execution, user-authored rules, and a simple workflow to register a `.sln`, run analyses, and understand whether the project passes or fails.

## Main features

- Register local solutions by `.sln` file path.
- Validate and refresh registered solution status.
- Create and edit custom rules through a schema-driven editor.
- Edit rules directly as raw JSON.
- Enable or disable rules per registered solution.
- Run Roslyn-based rule analysis.
- Persist analysis runs and violations.
- Run coverage analysis with `dotnet test --collect:"XPlat Code Coverage"`.
- Normalize coverage at project and class level.
- Show a dashboard with quality score, pass/fail status, and rule/coverage evidence.
- Expose a local API for agents to run quality analysis.
- Provide a CLI for headless analysis.

## Current stack

- .NET 10
- ASP.NET Core / Blazor Server
- Entity Framework Core
- SQLite
- Roslyn / MSBuildWorkspace
- Cobertura as the normalized coverage format
- xUnit, bUnit, and FluentAssertions for tests

## Repository structure

```txt
src/
  CodePass.Web/       Blazor web app, services, persistence, and analyzers.
  CodePass.Cli/       CLI for rule and coverage analysis.

tests/
  CodePass.Web.Tests/ Automated tests for the web app, services, and analyzers.

.planning/            Planning, requirements, roadmap, and state docs.
.planning/research/   Architecture and product research docs.
```

## Installation and usage

Prerequisites:

- .NET SDK 10 installed.
- Local filesystem access to the repositories/solutions you want to analyze.
- For coverage analysis, the target solution's test projects must be able to run with `dotnet test --collect:"XPlat Code Coverage"`.

Restore and validate the repository:

```bash
dotnet restore CodePass.sln
dotnet test CodePass.sln
```

### Web

The web app is the main way to use CodePass. It lets you register solutions, create rules, choose rules per solution, run analyses, and inspect the dashboard.

#### Run in development mode

```bash
dotnet run --project src/CodePass.Web --urls http://localhost:5000
```

Then open:

```txt
http://localhost:5000
```

By default, the web app uses SQLite with this connection string:

```json
"Data Source=codepass.db"
```

The app initializes the local database automatically on startup. The `codepass.db` file is created in the process working directory.

#### Publish and run the web app

```bash
dotnet publish src/CodePass.Web -c Release -o ./publish/codepass-web
```

Run the published app:

```bash
dotnet ./publish/codepass-web/CodePass.Web.dll --urls http://localhost:5000
```

#### Basic web workflow

1. Open the app in a browser.
2. Go to **Solutions** and register the absolute path to a local `.sln` file.
3. Go to **Rules** and create a rule with the form editor or raw JSON mode.
4. Go to **Rule Analysis** and enable the desired rules for the solution.
5. Run rule analysis.
6. Go to **Coverage Analysis** and run coverage analysis.
7. Open the **Dashboard** to see the score, pass/fail status, and evidence.

### CLI

The CLI lets you run analysis without opening the web UI. It is useful for local automation, scripts, and quick checks.

#### Run the CLI from the project

```bash
dotnet run --project src/CodePass.Cli -- analyze \
  --solution CodePass.sln \
  --rules ./rules.json \
  --coverage \
  --output codepass-quality.json
```

#### Install the CLI as a global tool

Create a local NuGet package:

```bash
dotnet pack src/CodePass.Cli -c Release -o ./artifacts
```

Install it as a global `dotnet tool`:

```bash
dotnet tool install --global CodePass.Tool --add-source ./artifacts
```

Then use the `codepass` command from any directory:

```bash
codepass analyze \
  --solution /path/to/App.sln \
  --rules /path/to/rules.json \
  --coverage \
  --output /path/to/codepass-quality.json
```

Update a local global-tool installation:

```bash
dotnet pack src/CodePass.Cli -c Release -o ./artifacts
dotnet tool update --global CodePass.Tool --add-source ./artifacts
```

Uninstall it:

```bash
dotnet tool uninstall --global CodePass.Tool
```

#### Main CLI options

- `--solution <path>`: path to the `.sln` file.
- `--rules <path>`: JSON file or directory with JSON rules.
- `--coverage`: run coverage analysis.
- `--output <path>`: save the result as JSON.
- `--min-line-coverage <n>`: minimum line coverage percentage.
- `--min-branch-coverage <n>`: minimum branch coverage percentage.
- `--pass-threshold <n>`: minimum score. Default: `80`.
- `--fail-on-rule-errors <bool>`: fail when `error` violations exist. Default: `true`.
- `--fail-on-rule-warnings <bool>`: fail when `warning` violations exist. Default: `false`.
- `--quiet`: reduce progress logs.

#### CLI examples

Rules only:

```bash
codepass analyze \
  --solution /path/to/App.sln \
  --rules /path/to/rules.json
```

Coverage only:

```bash
codepass analyze \
  --solution /path/to/App.sln \
  --coverage
```

Rules and coverage with a quality gate:

```bash
codepass analyze \
  --solution /path/to/App.sln \
  --rules /path/to/rules \
  --coverage \
  --min-line-coverage 80 \
  --min-branch-coverage 70 \
  --pass-threshold 85 \
  --output codepass-quality.json
```

The CLI returns exit code `0` when the quality gate passes and `1` when it fails.

## Custom rules

CodePass rules are always C# rules and use structured JSON. Do not include the `language` field in authored rule JSON.

Base shape:

```json
{
  "id": "CP1000",
  "title": "Rule title",
  "description": "Short description.",
  "kind": "method_metrics",
  "schemaVersion": "1.0",
  "severity": "warning",
  "enabled": true,
  "scope": {
    "projects": ["*"],
    "files": ["**/*.cs"],
    "excludeFiles": []
  },
  "parameters": {
    "maxLines": 50
  }
}
```

Currently supported kinds include:

- `syntax_presence`
- `forbidden_api_usage`
- `symbol_naming`
- `attribute_policy`
- `dependency_policy`
- `method_metrics`
- `class_metrics`
- `interface_metrics`
- `inheritance_contract_policy`
- `polymorphism_opportunity`
- `architecture_policy`
- `dependency_inversion_policy`
- `exception_handling`
- `async_policy`

See the full guide in [`rules-json-guide.md`](rules-json-guide.md).

## Local API for agents

When CodePass Web is running, local agents can use HTTP endpoints to list registered solutions and run quality analysis.

Flow:

```http
GET /api/agent-quality/solutions
POST /api/agent-quality/solutions/{registeredSolutionId}/analyze
```

The full documentation is in [`SKILL.md`](SKILL.md).

## Project Markdown files

### Main documentation

- [`README.md`](README.md): project overview, installation, usage, CLI, and documentation map.
- [`rules-json-guide.md`](rules-json-guide.md): current practical guide for the JSON rule format accepted by the web app and CLI.
- [`dotnet-lint-dsl-guide.md`](dotnet-lint-dsl-guide.md): conceptual study about modeling a structured DSL for .NET lint rules with Roslyn.
- [`SKILL.md`](SKILL.md): instructions for local agents that use CodePass quality endpoints.
- [`MEMORY.md`](MEMORY.md): operational memory with previous mistakes and preventive rules for future changes.

### Planning

- [`.planning/PROJECT.md`](.planning/PROJECT.md): product definition, core value, context, constraints, and key decisions.
- [`.planning/REQUIREMENTS.md`](.planning/REQUIREMENTS.md): v1/v2 requirements, out-of-scope items, and traceability.
- [`.planning/ROADMAP.md`](.planning/ROADMAP.md): roadmap phases and progress by phase/plan.
- [`.planning/STATE.md`](.planning/STATE.md): current project state, progress, accumulated decisions, and session continuity.

### Research

- [`.planning/research/SUMMARY.md`](.planning/research/SUMMARY.md): executive summary of product, architecture, stack, and risk research.
- [`.planning/research/ARCHITECTURE.md`](.planning/research/ARCHITECTURE.md): architecture research and recommended design.
- [`.planning/research/FEATURES.md`](.planning/research/FEATURES.md): expected features, differentiators, and anti-features.
- [`.planning/research/STACK.md`](.planning/research/STACK.md): stack research and rationale.
- [`.planning/research/PITFALLS.md`](.planning/research/PITFALLS.md): critical risks, warning signs, and mitigation strategies.

## Scope and key decisions

- v1 focuses only on C#/.NET.
- Active rules are user-authored; the product does not depend on built-in production rule packs.
- The rule `kind` catalog is closed and controlled by the engine.
- Rule JSON is validated against the supported fields for each `kind`.
- Rule analysis is based on Roslyn, not simple text search.
- The dashboard shows the current state; history and trends are future work.

## Status

The v1 plan is marked as complete in `.planning/STATE.md`, with the main phases implemented:

1. Registered solutions.
2. User-authored rules.
3. Rule analysis.
4. Coverage analysis.
5. Quality score dashboard.
