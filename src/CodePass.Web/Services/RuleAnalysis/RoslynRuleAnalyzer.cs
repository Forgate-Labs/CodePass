using System.Text.Json;
using System.Text.RegularExpressions;
using CodePass.Web.Services.Rules;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;

namespace CodePass.Web.Services.RuleAnalysis;

public sealed class RoslynRuleAnalyzer : IRuleAnalyzer
{
    private static readonly object MSBuildRegistrationLock = new();
    private static readonly SymbolDisplayFormat SymbolDisplayFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeContainingType,
        parameterOptions: SymbolDisplayParameterOptions.None,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    public async Task<IReadOnlyList<RuleAnalysisFinding>> AnalyzeAsync(
        string solutionPath,
        IReadOnlyList<AuthoredRuleDefinitionDto> rules,
        CancellationToken cancellationToken = default,
        IProgress<RuleAnalysisProgressDto>? progress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionPath);
        ArgumentNullException.ThrowIfNull(rules);

        if (rules.Count == 0)
        {
            Report(progress, RuleAnalysisProgressStage.Completed, "No enabled rules to analyze.", percentComplete: 100);
            return [];
        }

        Report(progress, RuleAnalysisProgressStage.LoadingSolution, "Registering MSBuild defaults...", percentComplete: 22);
        RegisterMSBuildDefaults();

        var solutionDirectory = Path.GetDirectoryName(Path.GetFullPath(solutionPath)) ?? Directory.GetCurrentDirectory();
        using var workspace = MSBuildWorkspace.Create(new Dictionary<string, string>
        {
            ["DesignTimeBuild"] = "true",
            ["BuildProjectReferences"] = "false"
        });

        Report(progress, RuleAnalysisProgressStage.LoadingSolution, "Opening solution with Roslyn/MSBuild...", percentComplete: 25, detail: solutionPath);
        var solution = await workspace.OpenSolutionAsync(solutionPath, cancellationToken: cancellationToken);
        var findings = new List<RuleAnalysisFinding>();
        var projects = solution.Projects.ToArray();
        var totalDocuments = projects
            .SelectMany(project => project.Documents)
            .Count(document => document.SourceCodeKind == SourceCodeKind.Regular
                && !string.IsNullOrWhiteSpace(document.FilePath)
                && document.FilePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase));
        var processedDocuments = 0;

        Report(
            progress,
            RuleAnalysisProgressStage.AnalyzingProjects,
            $"Analyzing {projects.Length} project(s) and {totalDocuments} C# document(s)...",
            percentComplete: 30,
            current: 0,
            total: totalDocuments);

        foreach (var project in projects)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Report(
                progress,
                RuleAnalysisProgressStage.AnalyzingProjects,
                $"Analyzing project {project.Name}...",
                percentComplete: CalculateAnalysisPercent(processedDocuments, totalDocuments),
                current: processedDocuments,
                total: totalDocuments,
                detail: project.FilePath ?? project.Name);

            foreach (var document in project.Documents.Where(document => document.SourceCodeKind == SourceCodeKind.Regular))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var filePath = document.FilePath;
                if (string.IsNullOrWhiteSpace(filePath) || !filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var relativePath = GetRelativePath(solutionDirectory, filePath);
                processedDocuments++;
                Report(
                    progress,
                    RuleAnalysisProgressStage.AnalyzingProjects,
                    $"Analyzing document {processedDocuments} of {totalDocuments}...",
                    percentComplete: CalculateAnalysisPercent(processedDocuments, totalDocuments),
                    current: processedDocuments,
                    total: totalDocuments,
                    detail: relativePath);

                var applicableRules = rules
                    .Where(rule => IsRuleInScope(rule, relativePath, project.Name))
                    .ToArray();

                if (applicableRules.Length == 0)
                {
                    continue;
                }

                var root = await document.GetSyntaxRootAsync(cancellationToken);
                if (root is null)
                {
                    continue;
                }

                SemanticModel? semanticModel = null;

                foreach (var rule in applicableRules)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    switch (rule.RuleKind.Trim().ToLowerInvariant())
                    {
                        case "syntax_presence":
                            findings.AddRange(AnalyzeSyntaxPresence(rule, root, relativePath));
                            break;
                        case "forbidden_api_usage":
                            semanticModel ??= await document.GetSemanticModelAsync(cancellationToken);
                            if (semanticModel is not null)
                            {
                                findings.AddRange(AnalyzeForbiddenApiUsage(rule, root, semanticModel, relativePath, cancellationToken));
                            }

                            break;
                        case "symbol_naming":
                            semanticModel ??= await document.GetSemanticModelAsync(cancellationToken);
                            if (semanticModel is not null)
                            {
                                findings.AddRange(AnalyzeSymbolNaming(rule, root, semanticModel, relativePath, cancellationToken));
                            }

                            break;
                        case "attribute_policy":
                            semanticModel ??= await document.GetSemanticModelAsync(cancellationToken);
                            if (semanticModel is not null)
                            {
                                findings.AddRange(AnalyzeAttributePolicy(rule, root, semanticModel, relativePath, cancellationToken));
                            }

                            break;
                        case "dependency_policy":
                            semanticModel ??= await document.GetSemanticModelAsync(cancellationToken);
                            if (semanticModel is not null)
                            {
                                findings.AddRange(AnalyzeDependencyPolicy(rule, root, semanticModel, relativePath, cancellationToken));
                            }

                            break;
                        case "method_metrics":
                            findings.AddRange(AnalyzeMethodMetrics(rule, root, relativePath, cancellationToken));
                            break;
                        case "exception_handling":
                            findings.AddRange(AnalyzeExceptionHandling(rule, root, relativePath, cancellationToken));
                            break;
                        case "async_policy":
                            findings.AddRange(AnalyzeAsyncPolicy(rule, root, relativePath, cancellationToken));
                            break;
                    }
                }
            }
        }

        Report(progress, RuleAnalysisProgressStage.AnalyzingProjects, "Finished analyzing source documents.", percentComplete: 90, current: processedDocuments, total: totalDocuments);
        return findings;
    }

    private static int CalculateAnalysisPercent(int processedDocuments, int totalDocuments)
    {
        if (totalDocuments <= 0)
        {
            return 90;
        }

        return Math.Clamp(30 + (int)Math.Round(processedDocuments * 60d / totalDocuments), 30, 90);
    }

    private static void Report(
        IProgress<RuleAnalysisProgressDto>? progress,
        RuleAnalysisProgressStage stage,
        string message,
        int? percentComplete = null,
        int? current = null,
        int? total = null,
        string? detail = null)
        => progress?.Report(new RuleAnalysisProgressDto(
            stage,
            message,
            percentComplete,
            current,
            total,
            detail));

    private static IReadOnlyList<RuleAnalysisFinding> AnalyzeSyntaxPresence(
        AuthoredRuleDefinitionDto rule,
        SyntaxNode root,
        string relativePath)
    {
        using var parametersDocument = ParseRuleJsonDocument(rule, rule.ParametersJson, "parameters JSON");
        var parameters = parametersDocument.RootElement;
        var mode = GetString(parameters, "mode") ?? "forbid";
        var syntaxKinds = GetStringArray(parameters, "syntaxKinds").Select(value => value.Trim().ToLowerInvariant()).ToArray();
        if (syntaxKinds.Length == 0)
        {
            return [];
        }

        var findings = new List<RuleAnalysisFinding>();
        foreach (var syntaxKind in syntaxKinds)
        {
            var nodes = FindSyntaxNodes(root, syntaxKind).ToArray();
            if (string.Equals(mode, "require", StringComparison.OrdinalIgnoreCase))
            {
                if (nodes.Length == 0)
                {
                    findings.Add(CreateFinding(
                        rule,
                        $"Rule '{rule.Code}' requires syntax construct '{syntaxKind}'.",
                        relativePath,
                        root.GetLocation()));
                }

                continue;
            }

            findings.AddRange(nodes.Select(node => CreateFinding(
                rule,
                $"Rule '{rule.Code}' forbids syntax construct '{syntaxKind}'.",
                relativePath,
                node.GetLocation())));
        }

        return findings;
    }

    private static IEnumerable<SyntaxNode> FindSyntaxNodes(SyntaxNode root, string syntaxKind)
    {
        return syntaxKind switch
        {
            "var" => root.DescendantNodes()
                .OfType<VariableDeclarationSyntax>()
                .Where(declaration => declaration.Type.IsVar),
            "goto" => root.DescendantNodes().OfType<GotoStatementSyntax>(),
            "expression_bodied_member" => root.DescendantNodes()
                .OfType<ArrowExpressionClauseSyntax>()
                .Where(arrow => arrow.Parent is BaseMethodDeclarationSyntax
                    or PropertyDeclarationSyntax
                    or IndexerDeclarationSyntax
                    or OperatorDeclarationSyntax
                    or ConversionOperatorDeclarationSyntax),
            "missing_braces" => FindMissingBraceStatements(root),
            _ => []
        };
    }

    private static IEnumerable<SyntaxNode> FindMissingBraceStatements(SyntaxNode root)
    {
        foreach (var statement in root.DescendantNodes().OfType<IfStatementSyntax>())
        {
            if (statement.Statement is not BlockSyntax)
            {
                yield return statement.Statement;
            }

            if (statement.Else?.Statement is not null and not BlockSyntax and not IfStatementSyntax)
            {
                yield return statement.Else.Statement;
            }
        }

        foreach (var statement in root.DescendantNodes().OfType<ForStatementSyntax>().Where(statement => statement.Statement is not BlockSyntax))
        {
            yield return statement.Statement;
        }

        foreach (var statement in root.DescendantNodes().OfType<ForEachStatementSyntax>().Where(statement => statement.Statement is not BlockSyntax))
        {
            yield return statement.Statement;
        }

        foreach (var statement in root.DescendantNodes().OfType<WhileStatementSyntax>().Where(statement => statement.Statement is not BlockSyntax))
        {
            yield return statement.Statement;
        }

        foreach (var statement in root.DescendantNodes().OfType<DoStatementSyntax>().Where(statement => statement.Statement is not BlockSyntax))
        {
            yield return statement.Statement;
        }
    }

    private static IReadOnlyList<RuleAnalysisFinding> AnalyzeForbiddenApiUsage(
        AuthoredRuleDefinitionDto rule,
        SyntaxNode root,
        SemanticModel semanticModel,
        string relativePath,
        CancellationToken cancellationToken)
    {
        using var parametersDocument = ParseRuleJsonDocument(rule, rule.ParametersJson, "parameters JSON");
        var parameters = parametersDocument.RootElement;
        var forbiddenSymbols = GetStringArray(parameters, "forbiddenSymbols").ToArray();
        if (forbiddenSymbols.Length == 0)
        {
            return [];
        }

        var alternatives = GetStringArray(parameters, "allowedAlternatives").ToArray();
        var findings = new List<RuleAnalysisFinding>();

        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var symbol = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol;
            var forbiddenSymbol = forbiddenSymbols.FirstOrDefault(forbidden => MatchesSymbol(symbol, forbidden));
            if (forbiddenSymbol is not null)
            {
                findings.Add(CreateFinding(rule, CreateForbiddenApiMessage(rule, forbiddenSymbol, alternatives), relativePath, invocation.GetLocation()));
            }
        }

        foreach (var objectCreation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var symbol = semanticModel.GetSymbolInfo(objectCreation.Type, cancellationToken).Symbol;
            var forbiddenSymbol = forbiddenSymbols.FirstOrDefault(forbidden => MatchesSymbol(symbol, forbidden));
            if (forbiddenSymbol is not null)
            {
                findings.Add(CreateFinding(rule, CreateForbiddenApiMessage(rule, forbiddenSymbol, alternatives), relativePath, objectCreation.GetLocation()));
            }
        }

        foreach (var memberAccess in root.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (memberAccess.Parent is InvocationExpressionSyntax invocation && invocation.Expression == memberAccess)
            {
                continue;
            }

            var symbol = semanticModel.GetSymbolInfo(memberAccess, cancellationToken).Symbol;
            var forbiddenSymbol = forbiddenSymbols.FirstOrDefault(forbidden => MatchesSymbol(symbol, forbidden));
            if (forbiddenSymbol is not null)
            {
                findings.Add(CreateFinding(rule, CreateForbiddenApiMessage(rule, forbiddenSymbol, alternatives), relativePath, memberAccess.GetLocation()));
            }
        }

        return findings;
    }

    private static IReadOnlyList<RuleAnalysisFinding> AnalyzeSymbolNaming(
        AuthoredRuleDefinitionDto rule,
        SyntaxNode root,
        SemanticModel semanticModel,
        string relativePath,
        CancellationToken cancellationToken)
    {
        using var parametersDocument = ParseRuleJsonDocument(rule, rule.ParametersJson, "parameters JSON");
        var parameters = parametersDocument.RootElement;
        var symbolKinds = GetStringArray(parameters, "symbolKinds").Select(value => value.Trim().ToLowerInvariant()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (symbolKinds.Count == 0)
        {
            return [];
        }

        var accessibilities = GetStringArray(parameters, "accessibilities").Select(value => value.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var capitalization = GetString(parameters, "capitalization");
        var requiredPrefix = GetString(parameters, "requiredPrefix") ?? string.Empty;
        var allowRegex = GetString(parameters, "allowRegex");
        var allowPattern = string.IsNullOrWhiteSpace(allowRegex) ? null : new Regex(allowRegex, RegexOptions.CultureInvariant);
        var findings = new List<RuleAnalysisFinding>();

        foreach (var candidate in EnumerateDeclaredSymbols(root, semanticModel, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!symbolKinds.Contains(candidate.Kind) || !AccessibilityMatches(candidate.Symbol, accessibilities))
            {
                continue;
            }

            if (NamePasses(candidate.Symbol.Name, requiredPrefix, capitalization, allowPattern))
            {
                continue;
            }

            findings.Add(CreateFinding(
                rule,
                $"Rule '{rule.Code}' requires {candidate.Kind} '{candidate.Symbol.Name}' to match the configured naming policy.",
                relativePath,
                candidate.Location));
        }

        return findings;
    }

    private static IReadOnlyList<RuleAnalysisFinding> AnalyzeAttributePolicy(
        AuthoredRuleDefinitionDto rule,
        SyntaxNode root,
        SemanticModel semanticModel,
        string relativePath,
        CancellationToken cancellationToken)
    {
        using var parametersDocument = ParseRuleJsonDocument(rule, rule.ParametersJson, "parameters JSON");
        var parameters = parametersDocument.RootElement;
        var mode = GetString(parameters, "mode") ?? "require";
        var targetKinds = GetStringArray(parameters, "targetKinds").Select(value => value.Trim().ToLowerInvariant()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var attributes = GetStringArray(parameters, "attributes").Select(NormalizeAttributeName).Where(value => value.Length > 0).ToArray();
        var matchInherited = GetBoolean(parameters, "matchInherited");

        if (targetKinds.Count == 0 || attributes.Length == 0)
        {
            return [];
        }

        var findings = new List<RuleAnalysisFinding>();
        foreach (var candidate in EnumerateDeclaredSymbols(root, semanticModel, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!targetKinds.Contains(candidate.Kind))
            {
                continue;
            }

            var matchingAttribute = FindMatchingAttribute(candidate.Symbol, attributes, matchInherited);
            if (string.Equals(mode, "forbid", StringComparison.OrdinalIgnoreCase))
            {
                if (matchingAttribute is not null)
                {
                    findings.Add(CreateFinding(
                        rule,
                        $"Rule '{rule.Code}' forbids attribute '{matchingAttribute}' on {candidate.Kind} '{candidate.Symbol.Name}'.",
                        relativePath,
                        candidate.Location));
                }

                continue;
            }

            if (matchingAttribute is null)
            {
                findings.Add(CreateFinding(
                    rule,
                    $"Rule '{rule.Code}' requires one of these attributes on {candidate.Kind} '{candidate.Symbol.Name}': {string.Join(", ", attributes)}.",
                    relativePath,
                    candidate.Location));
            }
        }

        return findings;
    }

    private static IReadOnlyList<RuleAnalysisFinding> AnalyzeDependencyPolicy(
        AuthoredRuleDefinitionDto rule,
        SyntaxNode root,
        SemanticModel semanticModel,
        string relativePath,
        CancellationToken cancellationToken)
    {
        using var parametersDocument = ParseRuleJsonDocument(rule, rule.ParametersJson, "parameters JSON");
        var parameters = parametersDocument.RootElement;
        var sourceNamespaces = GetStringArray(parameters, "sourceNamespaces").ToArray();
        var forbiddenNamespaces = GetStringArray(parameters, "forbiddenNamespaces").ToArray();
        var forbiddenTypes = GetStringArray(parameters, "forbiddenTypes").ToArray();

        if (forbiddenNamespaces.Length == 0 && forbiddenTypes.Length == 0)
        {
            return [];
        }

        var declaredNamespace = root.DescendantNodes()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .Select(ns => ns.Name.ToString())
            .FirstOrDefault() ?? string.Empty;
        if (sourceNamespaces.Length > 0 && !sourceNamespaces.Any(source => NamespaceMatches(declaredNamespace, source)))
        {
            return [];
        }

        var findings = new List<RuleAnalysisFinding>();
        var reportedLocations = new HashSet<TextSpan>();

        foreach (var usingDirective in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var namespaceName = usingDirective.Name?.ToString() ?? string.Empty;
            var forbiddenNamespace = forbiddenNamespaces.FirstOrDefault(forbidden => NamespaceMatches(namespaceName, forbidden));
            if (forbiddenNamespace is not null && reportedLocations.Add(usingDirective.Span))
            {
                findings.Add(CreateFinding(
                    rule,
                    $"Rule '{rule.Code}' forbids dependency on namespace '{forbiddenNamespace}'.",
                    relativePath,
                    usingDirective.GetLocation()));
            }
        }

        foreach (var node in root.DescendantNodes().Where(IsDependencySyntaxNode))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!reportedLocations.Add(node.Span))
            {
                continue;
            }

            var symbol = semanticModel.GetSymbolInfo(node, cancellationToken).Symbol;
            if (symbol is null)
            {
                symbol = semanticModel.GetTypeInfo(node, cancellationToken).Type;
            }

            var forbiddenType = forbiddenTypes.FirstOrDefault(forbidden => SymbolTypeMatches(symbol, forbidden));
            if (forbiddenType is not null)
            {
                findings.Add(CreateFinding(
                    rule,
                    $"Rule '{rule.Code}' forbids dependency on type '{forbiddenType}'.",
                    relativePath,
                    node.GetLocation()));
                continue;
            }

            var forbiddenNamespace = forbiddenNamespaces.FirstOrDefault(forbidden => SymbolNamespaceMatches(symbol, forbidden));
            if (forbiddenNamespace is not null)
            {
                findings.Add(CreateFinding(
                    rule,
                    $"Rule '{rule.Code}' forbids dependency on namespace '{forbiddenNamespace}'.",
                    relativePath,
                    node.GetLocation()));
            }
        }

        return findings;
    }

    private static IReadOnlyList<RuleAnalysisFinding> AnalyzeMethodMetrics(
        AuthoredRuleDefinitionDto rule,
        SyntaxNode root,
        string relativePath,
        CancellationToken cancellationToken)
    {
        using var parametersDocument = ParseRuleJsonDocument(rule, rule.ParametersJson, "parameters JSON");
        var parameters = parametersDocument.RootElement;
        var maxLines = GetInt(parameters, "maxLines") ?? 50;
        var maxParameters = GetInt(parameters, "maxParameters") ?? 5;
        var maxCyclomaticComplexity = GetInt(parameters, "maxCyclomaticComplexity") ?? 10;

        var findings = new List<RuleAnalysisFinding>();
        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var violations = new List<string>();
            var lineCount = CountLines(method.GetLocation());
            var parameterCount = method.ParameterList.Parameters.Count;
            var complexity = CalculateCyclomaticComplexity(method);

            if (maxLines > 0 && lineCount > maxLines)
            {
                violations.Add($"{lineCount} lines exceeds {maxLines}");
            }

            if (maxParameters > 0 && parameterCount > maxParameters)
            {
                violations.Add($"{parameterCount} parameters exceeds {maxParameters}");
            }

            if (maxCyclomaticComplexity > 0 && complexity > maxCyclomaticComplexity)
            {
                violations.Add($"cyclomatic complexity {complexity} exceeds {maxCyclomaticComplexity}");
            }

            if (violations.Count > 0)
            {
                findings.Add(CreateFinding(
                    rule,
                    $"Rule '{rule.Code}' method '{method.Identifier.ValueText}' violates metrics policy: {string.Join("; ", violations)}.",
                    relativePath,
                    method.Identifier.GetLocation()));
            }
        }

        return findings;
    }

    private static IReadOnlyList<RuleAnalysisFinding> AnalyzeExceptionHandling(
        AuthoredRuleDefinitionDto rule,
        SyntaxNode root,
        string relativePath,
        CancellationToken cancellationToken)
    {
        using var parametersDocument = ParseRuleJsonDocument(rule, rule.ParametersJson, "parameters JSON");
        var parameters = parametersDocument.RootElement;
        var forbidEmptyCatch = GetBoolean(parameters, "forbidEmptyCatch", defaultValue: true);
        var forbidCatchAll = GetBoolean(parameters, "forbidCatchAll", defaultValue: true);
        var forbidThrowEx = GetBoolean(parameters, "forbidThrowEx", defaultValue: true);
        var requireLogging = GetBoolean(parameters, "requireLogging");

        var findings = new List<RuleAnalysisFinding>();
        foreach (var catchClause in root.DescendantNodes().OfType<CatchClauseSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (forbidEmptyCatch && (catchClause.Block?.Statements.Count ?? 0) == 0)
            {
                findings.Add(CreateFinding(rule, $"Rule '{rule.Code}' forbids empty catch blocks.", relativePath, catchClause.GetLocation()));
            }

            if (forbidCatchAll && IsCatchAll(catchClause))
            {
                findings.Add(CreateFinding(rule, $"Rule '{rule.Code}' forbids catch-all exception handlers.", relativePath, catchClause.Declaration?.GetLocation() ?? catchClause.GetLocation()));
            }

            if (requireLogging && !CatchBlockHasLogging(catchClause))
            {
                findings.Add(CreateFinding(rule, $"Rule '{rule.Code}' requires logging inside catch blocks.", relativePath, catchClause.GetLocation()));
            }
        }

        if (forbidThrowEx)
        {
            foreach (var throwStatement in root.DescendantNodes().OfType<ThrowStatementSyntax>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (throwStatement.Expression is IdentifierNameSyntax identifier)
                {
                    findings.Add(CreateFinding(rule, $"Rule '{rule.Code}' forbids rethrowing captured exceptions with 'throw {identifier.Identifier.ValueText};'.", relativePath, throwStatement.GetLocation()));
                }
            }
        }

        return findings;
    }

    private static IReadOnlyList<RuleAnalysisFinding> AnalyzeAsyncPolicy(
        AuthoredRuleDefinitionDto rule,
        SyntaxNode root,
        string relativePath,
        CancellationToken cancellationToken)
    {
        using var parametersDocument = ParseRuleJsonDocument(rule, rule.ParametersJson, "parameters JSON");
        var parameters = parametersDocument.RootElement;
        var forbidAsyncVoid = GetBoolean(parameters, "forbidAsyncVoid", defaultValue: true);
        var requireCancellationToken = GetBoolean(parameters, "requireCancellationToken", defaultValue: true);
        var forbidBlockingCalls = GetBoolean(parameters, "forbidBlockingCalls", defaultValue: true);
        var findings = new List<RuleAnalysisFinding>();

        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var isAsync = method.Modifiers.Any(SyntaxKind.AsyncKeyword);
            if (!isAsync)
            {
                continue;
            }

            if (forbidAsyncVoid && method.ReturnType is PredefinedTypeSyntax predefinedType && predefinedType.Keyword.IsKind(SyntaxKind.VoidKeyword))
            {
                findings.Add(CreateFinding(rule, $"Rule '{rule.Code}' forbids async void method '{method.Identifier.ValueText}'.", relativePath, method.Identifier.GetLocation()));
            }

            if (requireCancellationToken
                && method.Modifiers.Any(SyntaxKind.PublicKeyword)
                && !method.ParameterList.Parameters.Any(ParameterIsCancellationToken))
            {
                findings.Add(CreateFinding(rule, $"Rule '{rule.Code}' requires public async method '{method.Identifier.ValueText}' to accept a CancellationToken.", relativePath, method.Identifier.GetLocation()));
            }
        }

        if (forbidBlockingCalls)
        {
            foreach (var node in root.DescendantNodes().Where(IsAsyncBlockingCall))
            {
                cancellationToken.ThrowIfCancellationRequested();
                findings.Add(CreateFinding(rule, $"Rule '{rule.Code}' forbids blocking async calls such as Result, Wait, or GetResult.", relativePath, node.GetLocation()));
            }
        }

        return findings;
    }

    private static IEnumerable<DeclaredSymbolCandidate> EnumerateDeclaredSymbols(SyntaxNode root, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        foreach (var declaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            if (semanticModel.GetDeclaredSymbol(declaration, cancellationToken) is { } symbol)
            {
                yield return new DeclaredSymbolCandidate("class", symbol, declaration.Identifier.GetLocation());
            }
        }

        foreach (var declaration in root.DescendantNodes().OfType<InterfaceDeclarationSyntax>())
        {
            if (semanticModel.GetDeclaredSymbol(declaration, cancellationToken) is { } symbol)
            {
                yield return new DeclaredSymbolCandidate("interface", symbol, declaration.Identifier.GetLocation());
            }
        }

        foreach (var declaration in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            if (semanticModel.GetDeclaredSymbol(declaration, cancellationToken) is { } symbol)
            {
                yield return new DeclaredSymbolCandidate("method", symbol, declaration.Identifier.GetLocation());
            }
        }

        foreach (var declaration in root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
        {
            if (semanticModel.GetDeclaredSymbol(declaration, cancellationToken) is { } symbol)
            {
                yield return new DeclaredSymbolCandidate("property", symbol, declaration.Identifier.GetLocation());
            }
        }

        foreach (var declaration in root.DescendantNodes().OfType<VariableDeclaratorSyntax>())
        {
            if (declaration.Parent?.Parent is FieldDeclarationSyntax && semanticModel.GetDeclaredSymbol(declaration, cancellationToken) is { } symbol)
            {
                yield return new DeclaredSymbolCandidate("field", symbol, declaration.Identifier.GetLocation());
            }
        }
    }

    private static bool NamePasses(string name, string requiredPrefix, string? capitalization, Regex? allowPattern)
    {
        if (allowPattern?.IsMatch(name) == true)
        {
            return true;
        }

        if (!string.IsNullOrEmpty(requiredPrefix) && !name.StartsWith(requiredPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var nameWithoutPrefix = !string.IsNullOrEmpty(requiredPrefix) && name.StartsWith(requiredPrefix, StringComparison.Ordinal)
            ? name[requiredPrefix.Length..]
            : name;

        if (string.IsNullOrEmpty(nameWithoutPrefix) || string.IsNullOrWhiteSpace(capitalization))
        {
            return true;
        }

        return capitalization.Trim() switch
        {
            "PascalCase" => char.IsUpper(nameWithoutPrefix[0]),
            "camelCase" => char.IsLower(nameWithoutPrefix[0]),
            _ => true
        };
    }

    private static bool AccessibilityMatches(ISymbol symbol, IReadOnlySet<string> accessibilities)
    {
        return accessibilities.Count == 0 || accessibilities.Contains(symbol.DeclaredAccessibility.ToString().ToLowerInvariant());
    }

    private static string? FindMatchingAttribute(ISymbol symbol, IReadOnlyList<string> expectedAttributes, bool matchInherited)
    {
        var attributeSymbols = symbol.GetAttributes()
            .Select(attribute => attribute.AttributeClass)
            .OfType<INamedTypeSymbol>()
            .ToList();

        if (matchInherited && symbol is INamedTypeSymbol namedType)
        {
            for (var baseType = namedType.BaseType; baseType is not null; baseType = baseType.BaseType)
            {
                attributeSymbols.AddRange(baseType.GetAttributes().Select(attribute => attribute.AttributeClass).OfType<INamedTypeSymbol>());
            }
        }

        foreach (var attributeSymbol in attributeSymbols)
        {
            var candidates = new[]
            {
                NormalizeAttributeName(attributeSymbol.Name),
                NormalizeAttributeName(attributeSymbol.ToDisplayString(SymbolDisplayFormat))
            };

            var matchingAttribute = expectedAttributes.FirstOrDefault(expected => candidates.Contains(expected, StringComparer.Ordinal));
            if (matchingAttribute is not null)
            {
                return matchingAttribute;
            }
        }

        return null;
    }

    private static string NormalizeAttributeName(string attributeName)
    {
        var normalized = NormalizeSymbolName(attributeName.Trim());
        const string suffix = "Attribute";
        return normalized.EndsWith(suffix, StringComparison.Ordinal)
            ? normalized[..^suffix.Length]
            : normalized;
    }

    private static bool NamespaceMatches(string namespaceName, string expectedNamespace)
    {
        if (string.IsNullOrWhiteSpace(namespaceName) || string.IsNullOrWhiteSpace(expectedNamespace))
        {
            return false;
        }

        var normalizedNamespace = namespaceName.Trim();
        var normalizedExpected = expectedNamespace.Trim();
        return string.Equals(normalizedNamespace, normalizedExpected, StringComparison.Ordinal)
            || normalizedNamespace.StartsWith(normalizedExpected + ".", StringComparison.Ordinal);
    }

    private static bool SymbolNamespaceMatches(ISymbol? symbol, string expectedNamespace)
    {
        if (symbol is null)
        {
            return false;
        }

        var namespaceName = symbol switch
        {
            INamedTypeSymbol typeSymbol => typeSymbol.ContainingNamespace?.ToDisplayString(),
            IMethodSymbol methodSymbol => methodSymbol.ContainingType?.ContainingNamespace?.ToDisplayString(),
            IPropertySymbol propertySymbol => propertySymbol.ContainingType?.ContainingNamespace?.ToDisplayString(),
            IFieldSymbol fieldSymbol => fieldSymbol.ContainingType?.ContainingNamespace?.ToDisplayString(),
            _ => symbol.ContainingNamespace?.ToDisplayString()
        };

        return NamespaceMatches(namespaceName ?? string.Empty, expectedNamespace);
    }

    private static bool SymbolTypeMatches(ISymbol? symbol, string expectedType)
    {
        if (symbol is null || string.IsNullOrWhiteSpace(expectedType))
        {
            return false;
        }

        var typeSymbol = symbol switch
        {
            INamedTypeSymbol namedType => namedType,
            IMethodSymbol methodSymbol => methodSymbol.ContainingType,
            IPropertySymbol propertySymbol => propertySymbol.ContainingType,
            IFieldSymbol fieldSymbol => fieldSymbol.ContainingType,
            _ => symbol.ContainingType
        };

        if (typeSymbol is null)
        {
            return false;
        }

        var normalizedExpected = NormalizeSymbolName(expectedType.Trim());
        var candidates = new[]
        {
            typeSymbol.Name,
            NormalizeSymbolName(typeSymbol.ToDisplayString(SymbolDisplayFormat))
        };

        return candidates.Any(candidate => string.Equals(candidate, normalizedExpected, StringComparison.Ordinal));
    }

    private static bool IsDependencySyntaxNode(SyntaxNode node)
    {
        return node switch
        {
            ObjectCreationExpressionSyntax => true,
            AttributeSyntax => true,
            IdentifierNameSyntax identifier => identifier.Parent is not MemberAccessExpressionSyntax,
            QualifiedNameSyntax => true,
            GenericNameSyntax => true,
            MemberAccessExpressionSyntax => true,
            _ => false
        };
    }

    private static int CountLines(Location location)
    {
        var lineSpan = location.GetLineSpan();
        return Math.Max(1, lineSpan.EndLinePosition.Line - lineSpan.StartLinePosition.Line + 1);
    }

    private static int CalculateCyclomaticComplexity(MethodDeclarationSyntax method)
    {
        var complexity = 1;
        foreach (var node in method.DescendantNodes())
        {
            complexity += node switch
            {
                IfStatementSyntax => 1,
                ForStatementSyntax => 1,
                ForEachStatementSyntax => 1,
                WhileStatementSyntax => 1,
                DoStatementSyntax => 1,
                CaseSwitchLabelSyntax => 1,
                ConditionalExpressionSyntax => 1,
                BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.LogicalAndExpression) || binary.IsKind(SyntaxKind.LogicalOrExpression) => 1,
                _ => 0
            };
        }

        return complexity;
    }

    private static bool IsCatchAll(CatchClauseSyntax catchClause)
    {
        var typeName = catchClause.Declaration?.Type.ToString();
        return string.IsNullOrWhiteSpace(typeName)
            || string.Equals(typeName, "Exception", StringComparison.Ordinal)
            || string.Equals(typeName, "System.Exception", StringComparison.Ordinal);
    }

    private static bool CatchBlockHasLogging(CatchClauseSyntax catchClause)
    {
        return catchClause.Block?.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Any(invocation =>
            {
                var expression = invocation.Expression.ToString();
                return expression.Contains("Log", StringComparison.Ordinal)
                    || expression.Contains("logger", StringComparison.OrdinalIgnoreCase);
            }) == true;
    }

    private static bool ParameterIsCancellationToken(ParameterSyntax parameter)
    {
        var typeName = parameter.Type?.ToString();
        return string.Equals(typeName, "CancellationToken", StringComparison.Ordinal)
            || string.Equals(typeName, "System.Threading.CancellationToken", StringComparison.Ordinal);
    }

    private static bool IsAsyncBlockingCall(SyntaxNode node)
    {
        return node switch
        {
            MemberAccessExpressionSyntax memberAccess when memberAccess.Name.Identifier.ValueText == "Result" => true,
            InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccess } when memberAccess.Name.Identifier.ValueText is "Wait" or "GetResult" => true,
            _ => false
        };
    }

    private static bool MatchesSymbol(ISymbol? symbol, string forbiddenSymbol)
    {
        if (symbol is null || string.IsNullOrWhiteSpace(forbiddenSymbol))
        {
            return false;
        }

        var normalizedForbidden = NormalizeSymbolName(forbiddenSymbol);
        var candidates = new List<string>
        {
            NormalizeSymbolName(symbol.ToDisplayString(SymbolDisplayFormat)),
            NormalizeSymbolName(symbol.OriginalDefinition.ToDisplayString(SymbolDisplayFormat))
        };

        if (symbol is IMethodSymbol methodSymbol)
        {
            candidates.Add(NormalizeSymbolName($"{methodSymbol.ContainingType.ToDisplayString(SymbolDisplayFormat)}.{methodSymbol.Name}"));
        }
        else if (symbol.ContainingType is not null)
        {
            candidates.Add(NormalizeSymbolName($"{symbol.ContainingType.ToDisplayString(SymbolDisplayFormat)}.{symbol.Name}"));
        }

        return candidates.Any(candidate => string.Equals(candidate, normalizedForbidden, StringComparison.Ordinal)
            || candidate.StartsWith(normalizedForbidden + "(", StringComparison.Ordinal));
    }

    private static string NormalizeSymbolName(string symbolName)
    {
        const string globalPrefix = "global::";
        return symbolName.StartsWith(globalPrefix, StringComparison.Ordinal)
            ? symbolName[globalPrefix.Length..]
            : symbolName;
    }

    private static string CreateForbiddenApiMessage(AuthoredRuleDefinitionDto rule, string forbiddenSymbol, IReadOnlyList<string> alternatives)
    {
        var message = $"Rule '{rule.Code}' forbids API usage '{forbiddenSymbol}'.";
        return alternatives.Count == 0
            ? message
            : $"{message} Allowed alternatives: {string.Join(", ", alternatives)}.";
    }

    private static RuleAnalysisFinding CreateFinding(
        AuthoredRuleDefinitionDto rule,
        string message,
        string relativePath,
        Location location)
    {
        var lineSpan = location.GetLineSpan();
        return new RuleAnalysisFinding(
            RuleId: rule.Id,
            RuleCode: rule.Code,
            RuleTitle: rule.Title,
            RuleKind: rule.RuleKind,
            Severity: rule.Severity,
            Message: message,
            RelativeFilePath: relativePath,
            StartLine: lineSpan.StartLinePosition.Line + 1,
            StartColumn: lineSpan.StartLinePosition.Character + 1,
            EndLine: lineSpan.EndLinePosition.Line + 1,
            EndColumn: lineSpan.EndLinePosition.Character + 1);
    }

    private static bool IsRuleInScope(AuthoredRuleDefinitionDto rule, string relativePath, string projectName)
    {
        using var scopeDocument = ParseRuleJsonDocument(rule, rule.ScopeJson, "scope JSON");
        var scope = scopeDocument.RootElement;
        var projects = GetStringArray(scope, "projects").DefaultIfEmpty("*").ToArray();
        var files = GetStringArray(scope, "files").DefaultIfEmpty("**/*.cs").ToArray();
        var excludeFiles = GetStringArray(scope, "excludeFiles").ToArray();

        return projects.Any(pattern => GlobMatches(pattern, projectName))
            && files.Any(pattern => GlobMatches(pattern, relativePath))
            && !excludeFiles.Any(pattern => GlobMatches(pattern, relativePath));
    }

    private static bool GlobMatches(string pattern, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        var normalizedPattern = NormalizePath(pattern.Trim());
        var normalizedPath = NormalizePath(relativePath);

        if (normalizedPattern == "*" || normalizedPattern == "**/*")
        {
            return true;
        }

        if (string.Equals(normalizedPattern, normalizedPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!normalizedPattern.Contains('*', StringComparison.Ordinal))
        {
            return normalizedPath.EndsWith(normalizedPattern, StringComparison.OrdinalIgnoreCase);
        }

        if (normalizedPattern == "**/*.cs")
        {
            return normalizedPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
        }

        var regexPattern = "^" + Regex.Escape(normalizedPattern)
            .Replace("\\*\\*/", "(?:.*/)?", StringComparison.Ordinal)
            .Replace("\\*", "[^/]*", StringComparison.Ordinal) + "$";
        return Regex.IsMatch(normalizedPath, regexPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string GetRelativePath(string rootPath, string filePath)
    {
        var relativePath = Path.GetRelativePath(rootPath, filePath);
        return NormalizePath(relativePath);
    }

    private static string NormalizePath(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static JsonDocument ParseRuleJsonDocument(AuthoredRuleDefinitionDto rule, string json, string payloadName)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"Rule '{rule.Code}' {payloadName} is invalid.", exception);
        }
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static bool GetBoolean(JsonElement element, string propertyName, bool defaultValue = false)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? property.GetBoolean()
                : defaultValue;
    }

    private static int? GetInt(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out var value)
                ? value
                : null;
    }

    private static IReadOnlyList<string> GetStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return property
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .OfType<string>()
            .ToArray();
    }

    private static void RegisterMSBuildDefaults()
    {
        if (MSBuildLocator.IsRegistered)
        {
            return;
        }

        lock (MSBuildRegistrationLock)
        {
            if (!MSBuildLocator.IsRegistered)
            {
                MSBuildLocator.RegisterDefaults();
            }
        }
    }

    private sealed record DeclaredSymbolCandidate(string Kind, ISymbol Symbol, Location Location);
}
