# Designing a DSL for .NET Lint Rules

## Goal

Build a .NET linter, inspired by SonarQube, where rules can be created and managed through a frontend in a simple and structured way.

The key idea is:

- do **not** use free-form natural language as the executable rule format
- use a **structured DSL** instead
- execute the rules on top of **Roslyn**
- define a **catalog of rule kinds**
- define a **parameter schema for each rule kind**
- let the frontend render forms dynamically from those schemas

---

## Core Recommendation

The best way to define lint rules for .NET is to separate the problem into three layers:

1. **Rule kind catalog**
2. **Rule instance definition**
3. **Rule executor**

### 1. Rule kind catalog

This is the list of rule families supported by your engine.

Example:

```json
{
  "kind": "disallow_implicit_typing",
  "engine": "roslyn",
  "language": "csharp",
  "analysisLevel": "syntax+semantic",
  "parameterSchema": { }
}
```

### 2. Rule instance definition

This is the rule created by the user in the frontend.

```json
{
  "id": "FG0001",
  "kind": "disallow_implicit_typing",
  "title": "Avoid implicit typing with var",
  "description": "Do not use 'var' for local variable declarations.",
  "category": "Style",
  "severity": "warning",
  "language": "csharp",
  "parameters": {
    "targets": ["local_declaration"],
    "allowInForLoop": false,
    "allowAnonymousTypes": true,
    "allowWhenTypeIsObvious": false
  }
}
```

### 3. Rule executor

This is the Roslyn-based implementation that knows how to interpret a given `kind`.

---

## Why Not Use Free-Form Text as the Rule Format?

Natural language is useful for UX, but not as the real executable format.

For example:

> "Do not allow var in local variable declarations except for anonymous types."

This is good for authoring, but bad as the main representation because it is:

- ambiguous
- hard to validate
- hard to version
- hard to execute precisely

A better approach is:

- allow the user to write a friendly description
- convert that into a structured internal model
- execute only the structured model

Example:

### Friendly input

```text
Do not allow var in local variables, except for anonymous types.
```

### Internal representation

```json
{
  "kind": "disallow_implicit_typing",
  "parameters": {
    "targets": ["local_declaration"],
    "allowAnonymousTypes": true
  }
}
```

---

## Use Roslyn as the Execution Engine

If your linter is for .NET and C#, Roslyn is the right foundation.

Roslyn gives you:

- syntax trees
- semantic model
- symbol analysis
- operation analysis
- diagnostics
- code fixes, if you want them later

For a rule such as “forbid `var`”, you should inspect syntax nodes such as:

- `LocalDeclarationStatementSyntax`
- possibly `ForEachStatementSyntax`
- possibly `UsingStatementSyntax` / using declarations
- maybe `ForStatementSyntax`

And use semantic checks for cases like anonymous types or "obvious" types.

Do **not** search the source code using string matching like `"var "`.

That would create false positives in:

- comments
- string literals
- variable names
- unrelated contexts

---

## The Right Mental Model: Metamodel Instead of Endless Ad-Hoc Rules

You should not try to create a single giant universal parameter model.

Instead:

- define **rule families**
- each family has its own **schema**
- rules are instances of those schemas
- the frontend renders each rule based on the schema

So the problem becomes:

- map **rule kinds**
- map **parameter schemas per kind**
- not “all parameters for all rules at once”

---

## Recommended Rule Families

A good starting point is 8 to 12 rule families.

### 1. `syntax_presence`

For forbidding or requiring syntax constructs.

Examples:

- forbid `var`
- forbid `goto`
- require braces in `if`
- forbid expression-bodied members

Typical parameters:

- `targets`
- `mode` (`forbid` or `require`)
- `syntaxKinds`
- `exceptions`
- `scopes`

---

### 2. `symbol_naming`

For naming conventions.

Examples:

- classes in PascalCase
- private fields must start with `_`
- interfaces must start with `I`

Typical parameters:

- `appliesToSymbolKinds`
- `appliesToAccessibility`
- `requiredPrefix`
- `requiredSuffix`
- `capitalization`
- `allowRegex`
- `excludeRegex`

---

### 3. `forbidden_api_usage`

For forbidding methods, types, namespaces, or attributes.

Examples:

- forbid `Console.WriteLine`
- forbid `DateTime.Now`
- forbid using a namespace in a given layer

Typical parameters:

- `forbiddenSymbols`
- `allowedAlternatives`
- `allowInFiles`
- `allowInProjects`
- `allowInTests`

---

### 4. `required_api_usage`

For requiring the use of something.

Examples:

- public async methods must accept `CancellationToken`
- controllers must have a certain attribute

Typical parameters:

- `targetSymbols`
- `requiredParameters`
- `requiredAttributes`
- `requiredBaseTypes`
- `requiredInterfaces`

---

### 5. `architecture_dependency`

For dependency rules between layers.

Examples:

- `Domain` must not reference `Infrastructure`
- `Application` must not depend on `UI`

Typical parameters:

- `sourceScopes`
- `forbiddenDependencies`
- `allowedDependencies`
- `dependencyKinds`

---

### 6. `attribute_policy`

For requiring or forbidding attributes.

Examples:

- DTO classes must have `[Serializable]`
- endpoints must have `[Authorize]`

Typical parameters:

- `targetSymbolKinds`
- `requiredAttributes`
- `forbiddenAttributes`
- `conditions`

---

### 7. `modifier_policy`

For rules about `public`, `private`, `sealed`, `static`, `readonly`, etc.

Examples:

- utility classes must be `static`
- public fields are forbidden

Typical parameters:

- `targetSymbolKinds`
- `requiredModifiers`
- `forbiddenModifiers`

---

### 8. `type_usage_policy`

For rules about type usage.

Examples:

- forbid `object`
- forbid `dynamic`
- forbid `List<T>` in public APIs
- forbid `var`

Typical parameters:

- `targetContexts`
- `forbiddenTypes`
- `requiredExplicitTypes`
- `exceptions`

---

### 9. `operation_pattern`

For richer semantic rules.

Examples:

- do not call `.Result` on tasks
- do not ignore return values
- do not use a specific async pattern

Typical parameters:

- `operationKinds`
- `pattern`
- `conditions`
- `exceptions`

---

### 10. `documentation_policy`

For documentation requirements.

Examples:

- public members must have XML docs

Typical parameters:

- `targetSymbolKinds`
- `requireXmlDocs`
- `minimumSummaryLength`
- `onlyPublic`

---

## Universal Parameter Groups

Instead of inventing different names everywhere, define a reusable vocabulary.

### Base parameters

Most rules can share these:

```json
{
  "enabled": true,
  "severity": "warning",
  "message": "Custom message",
  "category": "Style",
  "scopes": [],
  "excludeGeneratedCode": true,
  "excludePaths": [],
  "includePaths": [],
  "tags": []
}
```

### Target parameters

Common examples:

```json
{
  "targets": ["local_declaration"],
  "symbolKinds": ["class", "method", "property", "field"],
  "accessibilities": ["public", "internal"],
  "modifiers": ["static", "readonly"],
  "filePatterns": ["*.cs"],
  "namespaces": ["MyCompany.Domain"]
}
```

### Exception parameters

```json
{
  "exceptions": {
    "allowInTests": true,
    "allowInGeneratedCode": false,
    "allowInFilesMatching": ["*Designer.cs"],
    "allowForAnonymousTypes": true
  }
}
```

### Condition parameters

```json
{
  "conditions": [
    {
      "when": "containing_type_has_attribute",
      "attribute": "MySpecialAttribute"
    }
  ]
}
```

---

## Use JSON Schema per Rule Kind

This is what makes the system scalable and frontend-friendly.

Each `kind` should publish:

- friendly title
- description
- parameter definitions
- type of each parameter
- allowed enum values
- default values
- required fields
- conditional visibility if needed

Example for `disallow_implicit_typing`:

```json
{
  "$id": "rule-kinds/disallow_implicit_typing.schema.json",
  "type": "object",
  "properties": {
    "targets": {
      "type": "array",
      "items": {
        "type": "string",
        "enum": [
          "local_declaration",
          "foreach_variable",
          "for_initializer",
          "using_declaration"
        ]
      },
      "default": ["local_declaration"]
    },
    "allowAnonymousTypes": {
      "type": "boolean",
      "default": true
    },
    "allowWhenTypeIsObvious": {
      "type": "boolean",
      "default": false
    },
    "allowTupleDeconstruction": {
      "type": "boolean",
      "default": true
    },
    "allowedTypePatterns": {
      "type": "array",
      "items": { "type": "string" },
      "default": []
    }
  },
  "required": ["targets"]
}
```

This schema can drive the frontend form automatically.

---

## Keep Targets as a Closed Enum

Do not let `targets` be vague free-form strings forever.

Create a controlled set of values like:

```json
[
  "local_declaration",
  "foreach_variable",
  "for_initializer",
  "using_declaration",
  "lambda_parameter",
  "field_declaration",
  "property_declaration",
  "method_declaration",
  "class_declaration",
  "record_declaration"
]
```

Your engine then maps those targets internally to:

- syntax nodes
- symbol kinds
- operation kinds

This keeps the DSL stable and predictable.

---

## Separate User Concepts from Roslyn Internals

Do not expose Roslyn internals directly in the DSL.

### Bad

```json
{
  "analyzeSyntaxNodeKind": "LocalDeclarationStatementSyntax",
  "semanticCheckMode": "TypeInferenceAndAnonymousTypes"
}
```

### Good

```json
{
  "targets": ["local_declaration"],
  "allowAnonymousTypes": true
}
```

Rule of thumb:

- users configure **domain concepts**
- engine translates that to **Roslyn mechanics**

---

## How to Discover Rule Kinds and Parameters in Practice

A practical process:

### Step 1: Study real analyzers

Look at:

- Sonar-style rules
- StyleCop
- built-in .NET analyzers
- `.editorconfig` style options

### Step 2: Group rules by intent

Ask:

- is this naming?
- syntax?
- API usage?
- architectural dependency?
- modifiers?
- attributes?

### Step 3: extract common parameters

For example, syntax-related rules often share:

- target contexts
- allow in tests
- allow in generated code
- exceptions

### Step 4: normalize parameter names

Do not use inconsistent naming like:

- `allowInForLoop`
- `forLoopAllowed`
- `permitInFor`

Choose one pattern and repeat it.

---

## Recommended Domain Model in C#

### Rule kind definition

```csharp
public sealed class RuleKindDefinition
{
    public string Kind { get; init; } = default!;
    public string Title { get; init; } = default!;
    public string Description { get; init; } = default!;
    public string Language { get; init; } = "csharp";
    public string AnalysisLevel { get; init; } = default!;
    public JsonDocument ParameterSchema { get; init; } = default!;
}
```

### Rule instance definition

```csharp
public sealed class LintRuleDefinition
{
    public string Id { get; init; } = default!;
    public string Kind { get; init; } = default!;
    public string Title { get; init; } = default!;
    public string Description { get; init; } = default!;
    public string Category { get; init; } = default!;
    public string Severity { get; init; } = default!;
    public string Language { get; init; } = "csharp";
    public JsonDocument Parameters { get; init; } = default!;
}
```

### Rule executor

```csharp
public interface IRuleExecutor
{
    string Kind { get; }

    IReadOnlyList<LintIssue> Analyze(
        SyntaxNode root,
        SemanticModel semanticModel,
        LintRuleDefinition rule);
}
```

---

## Example: Rule for `var`

Instead of naming the rule `forbid_var`, prefer a broader and more future-proof family such as `disallow_implicit_typing`.

Example rule:

```json
{
  "id": "FG0001",
  "kind": "disallow_implicit_typing",
  "title": "Avoid implicit typing with var",
  "description": "Do not use 'var' for local variable declarations.",
  "category": "Style",
  "severity": "warning",
  "language": "csharp",
  "parameters": {
    "targets": [
      "local_declaration"
    ],
    "allowAnonymousTypes": true,
    "allowWhenTypeIsObvious": false,
    "allowInForLoop": false,
    "allowInForeach": false,
    "allowInUsingDeclaration": false,
    "excludeGeneratedCode": true
  }
}
```

This scales better because later you can expand the family to include rules about:

- `dynamic`
- explicit type policies
- other type inference restrictions

---

## How to Avoid Overengineering

Do not try to map everything at once.

### Phase 1

Create 6 to 10 fixed rule kinds.

### Phase 2

For each kind, create:

- parameter schema
- frontend form
- backend executor
- examples of valid and invalid code

### Phase 3

When a new rule appears, ask:

- does it fit in an existing kind?
- does it only require one more parameter?
- or does it really deserve a new family?

That keeps the DSL understandable.

---

## Recommended Base Rule Shape

```json
{
  "id": "FG0001",
  "kind": "disallow_implicit_typing",
  "title": "Avoid implicit typing with var",
  "description": "Do not use 'var' for local variable declarations.",
  "category": "Style",
  "severity": "warning",
  "language": "csharp",
  "scope": {
    "projects": ["*"],
    "files": ["**/*.cs"],
    "excludeFiles": ["**/*.g.cs", "**/*.designer.cs"]
  },
  "parameters": {}
}
```

This keeps the structure stable:

- metadata at the top
- scope separated
- behavior inside `parameters`

---

## Good Rule for Adding New Parameters

A parameter should only exist if it passes these three tests:

1. it represents a **real project-level decision**
2. it is something the user may realistically want to vary
3. it does not leak internal engine details

### Good

- `allowAnonymousTypes`

### Bad

- `semanticEvaluationMode: "symbol-first"`

---

## Frontend Strategy

The frontend should not be hardcoded for each individual rule.

Instead:

1. user selects a `kind`
2. frontend loads the schema for that kind
3. frontend renders controls dynamically
4. frontend saves the final JSON

Useful UI features:

- dropdown for `kind`
- severity selector
- generated JSON preview
- examples of violating and compliant code
- validation messages from JSON Schema

---

## Final Recommendation

The healthiest way to model this system is:

- create a **closed catalog of rule families**
- define **JSON Schema per family**
- maintain a **shared vocabulary** for targets, scopes, exceptions, and conditions
- hide Roslyn internals from the user
- let the frontend be driven by schema metadata

That gives you:

- validation
- versioning
- dynamic UI generation
- controlled growth
- compatibility with simple and advanced rules

---

## Practical Summary

If you are building a SonarQube-like linter for .NET:

- use **Roslyn** as the analysis engine
- use **structured JSON/YAML**, not free text, as the real rule format
- define **rule kinds**
- define **parameter schemas per rule kind**
- let the frontend render forms from those schemas
- keep user configuration focused on **domain concepts**, not Roslyn internals

For the `var` example specifically:

- model it as a rule kind like `disallow_implicit_typing`
- use parameters such as `targets`, `allowAnonymousTypes`, and `allowWhenTypeIsObvious`
- implement the executor on top of syntax + semantic analysis

---

## Example Final Rule

```json
{
  "id": "FG0001",
  "title": "Avoid implicit typing with var",
  "description": "Do not use 'var' for local variable declarations.",
  "category": "Style",
  "severity": "warning",
  "language": "csharp",
  "parameters": {
    "allowInForLoop": false,
    "allowAnonymousTypes": true,
    "allowWhenTypeIsObvious": false
  }
}
```

A stronger version would be:

```json
{
  "id": "FG0001",
  "kind": "disallow_implicit_typing",
  "title": "Avoid implicit typing with var",
  "description": "Do not use 'var' for local variable declarations.",
  "category": "Style",
  "severity": "warning",
  "language": "csharp",
  "parameters": {
    "targets": ["local_declaration"],
    "allowInForLoop": false,
    "allowAnonymousTypes": true,
    "allowWhenTypeIsObvious": false
  }
}
```

That version is more explicit, easier to validate, and easier to evolve.
