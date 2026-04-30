using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodePass.Web.Data.Entities;
using CodePass.Web.Services.CoverageAnalysis;
using CodePass.Web.Services.RuleAnalysis;
using CodePass.Web.Services.Rules;

var exitCode = await CodePassCli.RunAsync(args, Console.Out, Console.Error);
return exitCode;

internal static class CodePassCli
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length == 0 || Has(args, "--help") || Has(args, "-h"))
        {
            WriteHelp(output);
            return 0;
        }

        var command = args[0];
        if (!string.Equals(command, "analyze", StringComparison.OrdinalIgnoreCase))
        {
            error.WriteLine($"Comando desconhecido: {command}");
            WriteHelp(error);
            return 2;
        }

        try
        {
            var options = AnalyzeOptions.Parse(args.Skip(1).ToArray());
            return await RunAnalyzeAsync(options, output, error);
        }
        catch (ArgumentException exception)
        {
            error.WriteLine(exception.Message);
            return 2;
        }
        catch (Exception exception)
        {
            error.WriteLine($"CodePass CLI falhou: {exception.Message}");
            return 1;
        }
    }

    private static async Task<int> RunAnalyzeAsync(AnalyzeOptions options, TextWriter output, TextWriter error)
    {
        var solutionPath = Path.GetFullPath(options.SolutionPath);
        if (!File.Exists(solutionPath))
        {
            throw new ArgumentException($"Arquivo de solução não encontrado: {solutionPath}");
        }

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        var startedAtUtc = DateTimeOffset.UtcNow;
        RuleAnalysisCliResult? ruleAnalysis = null;
        CoverageAnalysisCliResult? coverageAnalysis = null;

        if (!string.IsNullOrWhiteSpace(options.RulesPath))
        {
            var rules = await RuleFileLoader.LoadAsync(options.RulesPath!, cancellation.Token);
            ruleAnalysis = await RunRuleAnalysisAsync(solutionPath, rules, options, output, cancellation.Token);
        }

        if (options.RunCoverage)
        {
            coverageAnalysis = await RunCoverageAnalysisAsync(solutionPath, options, output, cancellation.Token);
        }

        var qualityScore = QualityScoreCalculator.Calculate(ruleAnalysis, coverageAnalysis, options.PassThreshold);
        var result = new CodePassAnalysisResult(
            solutionPath,
            startedAtUtc,
            DateTimeOffset.UtcNow,
            ruleAnalysis,
            coverageAnalysis,
            qualityScore,
            QualityGate.Evaluate(ruleAnalysis, coverageAnalysis, qualityScore, options));

        if (!string.IsNullOrWhiteSpace(options.OutputPath))
        {
            var outputPath = Path.GetFullPath(options.OutputPath!);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? Directory.GetCurrentDirectory());
            await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(result, JsonOptions), cancellation.Token);
        }

        WriteSummary(result, output, error);
        return result.QualityGate.Passed ? 0 : 1;
    }

    private static async Task<RuleAnalysisCliResult> RunRuleAnalysisAsync(
        string solutionPath,
        IReadOnlyList<AuthoredRuleDefinitionDto> rules,
        AnalyzeOptions options,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        var enabledRules = rules.Where(rule => rule.IsEnabled).ToArray();
        if (!options.Quiet)
        {
            output.WriteLine($"Regras: carregadas {rules.Count}, habilitadas {enabledRules.Length}.");
        }

        var startedAtUtc = DateTimeOffset.UtcNow;
        try
        {
            var analyzer = new RoslynRuleAnalyzer();
            var progress = options.Quiet ? null : new Progress<RuleAnalysisProgressDto>(p => WriteProgress(output, "regras", p.Message, p.PercentComplete, p.Detail));
            var findings = await analyzer.AnalyzeAsync(solutionPath, enabledRules, cancellationToken, progress);
            var completedAtUtc = DateTimeOffset.UtcNow;
            var violations = findings.Select(finding => new RuleViolationCliResult(
                finding.RuleCode,
                finding.RuleTitle,
                finding.RuleKind,
                finding.Severity,
                finding.Message,
                finding.RelativeFilePath,
                finding.StartLine,
                finding.StartColumn,
                finding.EndLine,
                finding.EndColumn)).ToArray();

            return new RuleAnalysisCliResult(
                AnalysisStatus.Succeeded,
                startedAtUtc,
                completedAtUtc,
                enabledRules.Length,
                violations.Length,
                violations.Count(violation => violation.Severity == RuleSeverity.Error),
                violations.Count(violation => violation.Severity == RuleSeverity.Warning),
                violations.Count(violation => violation.Severity == RuleSeverity.Info),
                null,
                violations);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new RuleAnalysisCliResult(
                AnalysisStatus.Failed,
                startedAtUtc,
                DateTimeOffset.UtcNow,
                enabledRules.Length,
                0,
                0,
                0,
                0,
                exception.Message,
                []);
        }
    }

    private static async Task<CoverageAnalysisCliResult> RunCoverageAnalysisAsync(
        string solutionPath,
        AnalyzeOptions options,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        try
        {
            var analyzer = new DotNetCoverageAnalyzer();
            var progress = options.Quiet ? null : new Progress<CoverageAnalysisProgressDto>(p => WriteProgress(output, "cobertura", p.Message, p.PercentComplete, p.Detail));
            var result = await analyzer.AnalyzeAsync(solutionPath, cancellationToken, progress);
            var completedAtUtc = DateTimeOffset.UtcNow;
            var coveredLines = result.Projects.Sum(project => project.CoveredLines);
            var totalLines = result.Projects.Sum(project => project.TotalLines);
            var coveredBranches = result.Projects.Sum(project => project.CoveredBranches);
            var totalBranches = result.Projects.Sum(project => project.TotalBranches);

            return new CoverageAnalysisCliResult(
                AnalysisStatus.Succeeded,
                startedAtUtc,
                completedAtUtc,
                result.Projects.Count,
                result.Classes.Count,
                coveredLines,
                totalLines,
                Percent(coveredLines, totalLines),
                coveredBranches,
                totalBranches,
                Percent(coveredBranches, totalBranches),
                null,
                result.Projects,
                result.Classes);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new CoverageAnalysisCliResult(
                AnalysisStatus.Failed,
                startedAtUtc,
                DateTimeOffset.UtcNow,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                exception.Message,
                [],
                []);
        }
    }

    private static void WriteSummary(CodePassAnalysisResult result, TextWriter output, TextWriter error)
    {
        output.WriteLine();
        output.WriteLine("CodePass analysis");

        if (result.RuleAnalysis is not null)
        {
            output.WriteLine($"Regras: {result.RuleAnalysis.Status}, {result.RuleAnalysis.RuleCount} regras, {result.RuleAnalysis.TotalViolations} violações ({result.RuleAnalysis.ErrorCount} errors, {result.RuleAnalysis.WarningCount} warnings, {result.RuleAnalysis.InfoCount} info)");
            if (!string.IsNullOrWhiteSpace(result.RuleAnalysis.ErrorMessage))
            {
                output.WriteLine($"Falha em regras: {result.RuleAnalysis.ErrorMessage}");
            }
        }

        if (result.CoverageAnalysis is not null)
        {
            output.WriteLine($"Cobertura: {result.CoverageAnalysis.Status}, linhas {result.CoverageAnalysis.LineCoveragePercent:0.#}% ({result.CoverageAnalysis.CoveredLineCount}/{result.CoverageAnalysis.TotalLineCount}), branches {result.CoverageAnalysis.BranchCoveragePercent:0.#}% ({result.CoverageAnalysis.CoveredBranchCount}/{result.CoverageAnalysis.TotalBranchCount})");
            if (!string.IsNullOrWhiteSpace(result.CoverageAnalysis.ErrorMessage))
            {
                output.WriteLine($"Falha em cobertura: {result.CoverageAnalysis.ErrorMessage}");
            }
        }

        output.WriteLine($"Quality score: {result.QualityScore.Score:0.#}, status {result.QualityScore.Status}");
        output.WriteLine($"Quality gate: {(result.QualityGate.Passed ? "Pass" : "Fail")}");

        if (result.QualityGate.BlockingReasons.Count > 0)
        {
            foreach (var reason in result.QualityGate.BlockingReasons)
            {
                error.WriteLine($"- {reason}");
            }
        }
    }

    private static void WriteProgress(TextWriter output, string area, string message, int? percentComplete, string? detail)
    {
        var percent = percentComplete is null ? string.Empty : $" {percentComplete}%";
        output.WriteLine($"[{area}{percent}] {message}{(string.IsNullOrWhiteSpace(detail) ? string.Empty : $" ({detail})")}");
    }

    private static double Percent(int covered, int total)
    {
        return total <= 0 ? 0 : Math.Round(covered * 100d / total, 1, MidpointRounding.AwayFromZero);
    }

    private static bool Has(string[] args, string option)
    {
        return args.Any(arg => string.Equals(arg, option, StringComparison.OrdinalIgnoreCase));
    }

    private static void WriteHelp(TextWriter writer)
    {
        writer.WriteLine("""
CodePass CLI

Uso:
  codepass analyze --solution <path.sln> [--rules <arquivo-ou-pasta>] [--coverage] [opções]

Opções:
  --solution <path>              Caminho para a solution .sln. Obrigatório.
  --rules <path>                 Pasta com *.json ou arquivo JSON com uma regra/array de regras.
  --coverage                     Executa dotnet test com XPlat Code Coverage.
  --output <path>                Salva resultado JSON.
  --min-line-coverage <n>        Percentual mínimo de cobertura de linhas.
  --min-branch-coverage <n>      Percentual mínimo de cobertura de branches.
  --pass-threshold <n>           Score mínimo. Padrão: 80.
  --fail-on-rule-errors <bool>   Falha se houver violações Error. Padrão: true.
  --fail-on-rule-warnings <bool> Falha se houver violações Warning. Padrão: false.
  --quiet                        Reduz logs de progresso.

Exemplo:
  codepass analyze --solution CodePass.sln --coverage --rules .codepass/rules --output codepass-quality.json
""");
    }
}

internal sealed record AnalyzeOptions(
    string SolutionPath,
    string? RulesPath,
    bool RunCoverage,
    string? OutputPath,
    double? MinLineCoverage,
    double? MinBranchCoverage,
    double PassThreshold,
    bool FailOnRuleErrors,
    bool FailOnRuleWarnings,
    bool Quiet)
{
    public static AnalyzeOptions Parse(string[] args)
    {
        string? solution = null;
        string? rules = null;
        var coverage = false;
        string? output = null;
        double? minLineCoverage = null;
        double? minBranchCoverage = null;
        var passThreshold = 80d;
        var failOnRuleErrors = true;
        var failOnRuleWarnings = false;
        var quiet = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--solution":
                    solution = RequireValue(args, ref i, arg);
                    break;
                case "--rules":
                    rules = RequireValue(args, ref i, arg);
                    break;
                case "--coverage":
                    coverage = true;
                    break;
                case "--output":
                    output = RequireValue(args, ref i, arg);
                    break;
                case "--min-line-coverage":
                    minLineCoverage = ParseDouble(RequireValue(args, ref i, arg), arg);
                    break;
                case "--min-branch-coverage":
                    minBranchCoverage = ParseDouble(RequireValue(args, ref i, arg), arg);
                    break;
                case "--pass-threshold":
                    passThreshold = ParseDouble(RequireValue(args, ref i, arg), arg);
                    break;
                case "--fail-on-rule-errors":
                    failOnRuleErrors = ParseBool(RequireValue(args, ref i, arg), arg);
                    break;
                case "--fail-on-rule-warnings":
                    failOnRuleWarnings = ParseBool(RequireValue(args, ref i, arg), arg);
                    break;
                case "--quiet":
                    quiet = true;
                    break;
                default:
                    throw new ArgumentException($"Opção desconhecida: {arg}");
            }
        }

        if (string.IsNullOrWhiteSpace(solution))
        {
            throw new ArgumentException("Informe --solution <path.sln>.");
        }

        if (string.IsNullOrWhiteSpace(rules) && !coverage)
        {
            throw new ArgumentException("Informe --rules, --coverage ou ambos.");
        }

        return new AnalyzeOptions(solution, rules, coverage, output, minLineCoverage, minBranchCoverage, passThreshold, failOnRuleErrors, failOnRuleWarnings, quiet);
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Informe um valor para {option}.");
        }

        index++;
        return args[index];
    }

    private static double ParseDouble(string value, string option)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new ArgumentException($"Valor inválido para {option}: {value}");
        }

        return parsed;
    }

    private static bool ParseBool(string value, string option)
    {
        if (!bool.TryParse(value, out var parsed))
        {
            throw new ArgumentException($"Valor inválido para {option}: {value}. Use true ou false.");
        }

        return parsed;
    }
}

internal static class RuleFileLoader
{
    private static readonly JsonSerializerOptions NormalizedJsonOptions = new() { WriteIndented = false };

    public static async Task<IReadOnlyList<AuthoredRuleDefinitionDto>> LoadAsync(string path, CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(path);
        if (Directory.Exists(fullPath))
        {
            var files = Directory.EnumerateFiles(fullPath, "*.json", SearchOption.AllDirectories)
                .OrderBy(file => file, StringComparer.Ordinal)
                .ToArray();

            var rules = new List<AuthoredRuleDefinitionDto>();
            foreach (var file in files)
            {
                rules.AddRange(await LoadFileAsync(file, cancellationToken));
            }

            return rules;
        }

        if (File.Exists(fullPath))
        {
            return await LoadFileAsync(fullPath, cancellationToken);
        }

        throw new ArgumentException($"Caminho de regras não encontrado: {fullPath}");
    }

    private static async Task<IReadOnlyList<AuthoredRuleDefinitionDto>> LoadFileAsync(string file, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(file);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            return document.RootElement.EnumerateArray().Select((element, index) => ParseRule(element, $"{file}[{index}]")).ToArray();
        }

        if (document.RootElement.ValueKind == JsonValueKind.Object)
        {
            return [ParseRule(document.RootElement, file)];
        }

        throw new InvalidOperationException($"Arquivo de regra deve conter objeto ou array JSON: {file}");
    }

    private static AuthoredRuleDefinitionDto ParseRule(JsonElement element, string source)
    {
        var language = GetOptionalString(element, "language") ?? "csharp";
        if (!string.Equals(language, "csharp", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Regra em {source} deve usar language 'csharp'.");
        }

        var code = GetRequiredString(element, "id", "code");
        var title = GetRequiredString(element, "title");
        var description = GetOptionalString(element, "description");
        var kind = GetRequiredString(element, "kind", "ruleKind");
        var schemaVersion = GetOptionalString(element, "schemaVersion") ?? "1.0";
        var severityText = GetOptionalString(element, "severity") ?? "Warning";
        var enabled = GetOptionalBoolean(element, "enabled") ?? GetOptionalBoolean(element, "isEnabled") ?? true;
        var scope = GetObjectOrDefault(element, "scope");
        var parameters = GetObjectOrDefault(element, "parameters");

        if (!Enum.TryParse<RuleSeverity>(severityText, ignoreCase: true, out var severity) || !Enum.IsDefined(severity))
        {
            throw new InvalidOperationException($"Regra '{code}' em {source} possui severity inválida: {severityText}.");
        }

        var raw = JsonSerializer.Serialize(element, NormalizedJsonOptions);
        return new AuthoredRuleDefinitionDto(
            Guid.NewGuid(),
            code,
            title,
            description,
            kind,
            schemaVersion,
            severity,
            JsonSerializer.Serialize(scope, NormalizedJsonOptions),
            JsonSerializer.Serialize(parameters, NormalizedJsonOptions),
            raw,
            enabled,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    private static JsonElement GetObjectOrDefault(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return JsonSerializer.SerializeToElement(new { });
        }

        if (property.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"Propriedade '{propertyName}' deve ser objeto JSON.");
        }

        return property.Clone();
    }

    private static string GetRequiredString(JsonElement element, params string[] propertyNames)
    {
        var value = propertyNames.Select(name => GetOptionalString(element, name)).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Regra JSON sem propriedade obrigatória: {string.Join(" ou ", propertyNames)}.");
        }

        return value.Trim();
    }

    private static string? GetOptionalString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static bool? GetOptionalBoolean(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? property.GetBoolean()
            : null;
    }
}

internal static class QualityScoreCalculator
{
    public static QualityScoreCliResult Calculate(RuleAnalysisCliResult? ruleAnalysis, CoverageAnalysisCliResult? coverageAnalysis, double passThreshold)
    {
        var ruleContribution = CalculateRuleContribution(ruleAnalysis, coverageAnalysis?.TotalLineCount);
        var coverageContribution = CalculateCoverageContribution(coverageAnalysis);
        var executedContributions = new List<double>();
        if (ruleAnalysis is not null)
        {
            executedContributions.Add(ruleContribution.EarnedPoints);
        }

        if (coverageAnalysis is not null)
        {
            executedContributions.Add(coverageContribution.EarnedPoints);
        }

        var score = executedContributions.Count == 0 ? 0 : Round(executedContributions.Average());
        var blockingReasons = ruleContribution.BlockingReasons.Concat(coverageContribution.BlockingReasons).ToArray();
        var status = score >= passThreshold
            && (ruleAnalysis is null || ruleAnalysis.Status == AnalysisStatus.Succeeded)
            && (coverageAnalysis is null || coverageAnalysis.Status == AnalysisStatus.Succeeded)
            && (ruleAnalysis?.ErrorCount ?? 0) == 0
                ? QualityScoreStatus.Pass
                : QualityScoreStatus.Fail;

        return new QualityScoreCliResult(score, status, ruleContribution, coverageContribution, blockingReasons);
    }

    private static QualityRuleContributionCliResult CalculateRuleContribution(RuleAnalysisCliResult? run, int? totalLineCount)
    {
        if (run is null)
        {
            return new QualityRuleContributionCliResult(100, 0, "Missing", 0, 0, 0, 0, "Análise de regras não executada.", []);
        }

        if (run.Status != AnalysisStatus.Succeeded)
        {
            var reason = string.IsNullOrWhiteSpace(run.ErrorMessage) ? "Rule-analysis evidence failed." : $"Rule-analysis evidence failed: {run.ErrorMessage}";
            return new QualityRuleContributionCliResult(100, 0, "Failed", run.ErrorCount, run.WarningCount, run.InfoCount, run.TotalViolations, reason, [reason]);
        }

        var weighted = run.ErrorCount * 5d + run.WarningCount * 2d + run.InfoCount * 0.5d;
        var effectiveLineCount = Math.Max(totalLineCount.GetValueOrDefault(), 1000);
        var affectedLines = run.Violations.Select(v => new { v.FilePath, v.StartLine }).Distinct().Count();
        var densityPenalty = weighted / effectiveLineCount * 1000;
        var affectedLinePenalty = affectedLines / (double)effectiveLineCount * 100;
        var volumePenalty = Math.Log2(1 + weighted) * 3;
        var score = 100 - Math.Max(Math.Max(densityPenalty, affectedLinePenalty), volumePenalty);
        if (run.ErrorCount > 0) score = Math.Min(score, 89);
        else if (run.WarningCount > 0) score = Math.Min(score, 95);
        else if (run.InfoCount > 0) score = Math.Min(score, 98);

        return new QualityRuleContributionCliResult(100, Round(Clamp(score)), "Succeeded", run.ErrorCount, run.WarningCount, run.InfoCount, run.TotalViolations, $"{run.TotalViolations} violações.", []);
    }

    private static QualityCoverageContributionCliResult CalculateCoverageContribution(CoverageAnalysisCliResult? run)
    {
        if (run is null)
        {
            return new QualityCoverageContributionCliResult(100, 0, "Missing", null, null, null, "Análise de cobertura não executada.", []);
        }

        if (run.Status != AnalysisStatus.Succeeded)
        {
            var reason = string.IsNullOrWhiteSpace(run.ErrorMessage) ? "Coverage-analysis evidence failed." : $"Coverage-analysis evidence failed: {run.ErrorMessage}";
            return new QualityCoverageContributionCliResult(100, 0, "Failed", run.LineCoveragePercent, run.CoveredLineCount, run.TotalLineCount, reason, [reason]);
        }

        return new QualityCoverageContributionCliResult(100, Round(Clamp(run.LineCoveragePercent)), "Succeeded", run.LineCoveragePercent, run.CoveredLineCount, run.TotalLineCount, $"{run.LineCoveragePercent:0.#}% line coverage.", []);
    }

    private static double Clamp(double value) => Math.Min(Math.Max(value, 0), 100);
    private static double Round(double value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);
}

internal static class QualityGate
{
    public static QualityGateCliResult Evaluate(RuleAnalysisCliResult? rules, CoverageAnalysisCliResult? coverage, QualityScoreCliResult score, AnalyzeOptions options)
    {
        var reasons = new List<string>();
        if (rules?.Status == AnalysisStatus.Failed) reasons.Add($"Análise de regras falhou: {rules.ErrorMessage}");
        if (coverage?.Status == AnalysisStatus.Failed) reasons.Add($"Análise de cobertura falhou: {coverage.ErrorMessage}");
        if (options.FailOnRuleErrors && rules?.ErrorCount > 0) reasons.Add($"Análise de regras encontrou {rules.ErrorCount} violação(ões) Error.");
        if (options.FailOnRuleWarnings && rules?.WarningCount > 0) reasons.Add($"Análise de regras encontrou {rules.WarningCount} violação(ões) Warning.");
        if (options.MinLineCoverage is not null && coverage is not null && coverage.LineCoveragePercent < options.MinLineCoverage) reasons.Add($"Cobertura de linhas {coverage.LineCoveragePercent:0.#}% abaixo do mínimo {options.MinLineCoverage:0.#}%.");
        if (options.MinBranchCoverage is not null && coverage is not null && coverage.BranchCoveragePercent < options.MinBranchCoverage) reasons.Add($"Cobertura de branches {coverage.BranchCoveragePercent:0.#}% abaixo do mínimo {options.MinBranchCoverage:0.#}%.");
        if (score.Status == QualityScoreStatus.Fail) reasons.Add($"Quality score {score.Score:0.#} abaixo do threshold {options.PassThreshold:0.#} ou evidências obrigatórias falharam.");

        return new QualityGateCliResult(reasons.Count == 0, reasons);
    }
}

internal enum AnalysisStatus { Succeeded, Failed }
internal enum QualityScoreStatus { Fail, Pass }

internal sealed record CodePassAnalysisResult(string SolutionPath, DateTimeOffset StartedAtUtc, DateTimeOffset CompletedAtUtc, RuleAnalysisCliResult? RuleAnalysis, CoverageAnalysisCliResult? CoverageAnalysis, QualityScoreCliResult QualityScore, QualityGateCliResult QualityGate);
internal sealed record RuleAnalysisCliResult(AnalysisStatus Status, DateTimeOffset StartedAtUtc, DateTimeOffset CompletedAtUtc, int RuleCount, int TotalViolations, int ErrorCount, int WarningCount, int InfoCount, string? ErrorMessage, IReadOnlyList<RuleViolationCliResult> Violations);
internal sealed record RuleViolationCliResult(string RuleCode, string RuleTitle, string RuleKind, RuleSeverity Severity, string Message, string FilePath, int StartLine, int StartColumn, int EndLine, int EndColumn);
internal sealed record CoverageAnalysisCliResult(AnalysisStatus Status, DateTimeOffset StartedAtUtc, DateTimeOffset CompletedAtUtc, int ProjectCount, int ClassCount, int CoveredLineCount, int TotalLineCount, double LineCoveragePercent, int CoveredBranchCount, int TotalBranchCount, double BranchCoveragePercent, string? ErrorMessage, IReadOnlyList<CodePass.Web.Services.CoverageAnalysis.CoverageProjectSummary> ProjectSummaries, IReadOnlyList<CodePass.Web.Services.CoverageAnalysis.CoverageClassCoverage> ClassCoverages);
internal sealed record QualityScoreCliResult(double Score, QualityScoreStatus Status, QualityRuleContributionCliResult RuleContribution, QualityCoverageContributionCliResult CoverageContribution, IReadOnlyList<string> BlockingReasons);
internal sealed record QualityRuleContributionCliResult(double MaxPoints, double EarnedPoints, string EvidenceStatus, int ErrorCount, int WarningCount, int InfoCount, int TotalViolations, string Summary, IReadOnlyList<string> BlockingReasons);
internal sealed record QualityCoverageContributionCliResult(double MaxPoints, double EarnedPoints, string EvidenceStatus, double? LineCoveragePercent, int? CoveredLineCount, int? TotalLineCount, string Summary, IReadOnlyList<string> BlockingReasons);
internal sealed record QualityGateCliResult(bool Passed, IReadOnlyList<string> BlockingReasons);
