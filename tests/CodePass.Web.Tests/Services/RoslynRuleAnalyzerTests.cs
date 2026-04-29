using System.Diagnostics;
using CodePass.Web.Data.Entities;
using CodePass.Web.Services.RuleAnalysis;
using CodePass.Web.Services.Rules;
using FluentAssertions;

namespace CodePass.Web.Tests.Services;

public sealed class RoslynRuleAnalyzerTests
{
    [Fact]
    public async Task AnalyzeAsync_ShouldReportForbiddenVarSyntaxWithSeverityAndLocation()
    {
        using var fixture = await RoslynSolutionFixture.CreateAsync(
            """
            namespace SampleProject;

            public sealed class PolicyTarget
            {
                public void Run()
                {
                    var value = 42;
                }
            }
            """);

        var rule = CreateRule(
            code: "CP1001",
            title: "Avoid var",
            kind: "syntax_presence",
            severity: RuleSeverity.Warning,
            parametersJson: "{\"mode\":\"forbid\",\"targets\":[\"local_declaration\"],\"syntaxKinds\":[\"var\"]}");

        var findings = await new RoslynRuleAnalyzer().AnalyzeAsync(fixture.SolutionPath, [rule]);

        findings.Should().ContainSingle();
        findings[0].RuleCode.Should().Be("CP1001");
        findings[0].RuleTitle.Should().Be("Avoid var");
        findings[0].RuleKind.Should().Be("syntax_presence");
        findings[0].Severity.Should().Be(RuleSeverity.Warning);
        findings[0].RelativeFilePath.Should().Be("SampleProject/PolicyTarget.cs");
        findings[0].Message.Should().Contain("var");
        AssertHasLocation(findings[0]);
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldReportForbiddenApiUsageWithAuthoredSeverity()
    {
        using var fixture = await RoslynSolutionFixture.CreateAsync(
            """
            namespace SampleProject;

            public sealed class PolicyTarget
            {
                public void Run()
                {
                    System.Console.WriteLine("Hello");
                }
            }
            """);

        var rule = CreateRule(
            code: "CP1002",
            title: "No console writes",
            kind: "forbidden_api_usage",
            severity: RuleSeverity.Error,
            parametersJson: "{\"forbiddenSymbols\":[\"System.Console.WriteLine\"],\"allowedAlternatives\":[\"ILogger.LogInformation\"]}");

        var findings = await new RoslynRuleAnalyzer().AnalyzeAsync(fixture.SolutionPath, [rule]);

        findings.Should().ContainSingle();
        findings[0].Severity.Should().Be(RuleSeverity.Error);
        findings[0].RuleCode.Should().Be("CP1002");
        findings[0].Message.Should().Contain("System.Console.WriteLine");
        findings[0].Message.Should().Contain("ILogger.LogInformation");
        findings[0].RelativeFilePath.Should().Be("SampleProject/PolicyTarget.cs");
        AssertHasLocation(findings[0]);
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldReportPrivateFieldNamingViolationWithAuthoredSeverity()
    {
        using var fixture = await RoslynSolutionFixture.CreateAsync(
            """
            namespace SampleProject;

            public sealed class PolicyTarget
            {
                private readonly string badName = "value";
            }
            """);

        var rule = CreateRule(
            code: "CP1003",
            title: "Private fields use underscore",
            kind: "symbol_naming",
            severity: RuleSeverity.Info,
            parametersJson: "{\"symbolKinds\":[\"field\"],\"accessibilities\":[\"private\"],\"capitalization\":\"camelCase\",\"requiredPrefix\":\"_\",\"allowRegex\":\"\"}");

        var findings = await new RoslynRuleAnalyzer().AnalyzeAsync(fixture.SolutionPath, [rule]);

        findings.Should().ContainSingle();
        findings[0].Severity.Should().Be(RuleSeverity.Info);
        findings[0].RuleCode.Should().Be("CP1003");
        findings[0].Message.Should().Contain("badName");
        findings[0].RelativeFilePath.Should().Be("SampleProject/PolicyTarget.cs");
        AssertHasLocation(findings[0]);
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldReportMissingRequiredAttribute()
    {
        using var fixture = await RoslynSolutionFixture.CreateAsync(
            """
            namespace SampleProject;

            public sealed class PolicyTarget
            {
            }
            """);

        var rule = CreateRule(
            code: "CP1007",
            title: "Classes require marker attribute",
            kind: "attribute_policy",
            severity: RuleSeverity.Warning,
            parametersJson: "{\"mode\":\"require\",\"targetKinds\":[\"class\"],\"attributes\":[\"Serializable\"],\"matchInherited\":false}",
            scopeJson: "{\"files\":[\"SampleProject/PolicyTarget.cs\"],\"excludeFiles\":[]}");

        var findings = await new RoslynRuleAnalyzer().AnalyzeAsync(fixture.SolutionPath, [rule]);

        findings.Should().ContainSingle();
        findings[0].RuleKind.Should().Be("attribute_policy");
        findings[0].Message.Should().Contain("requires");
        findings[0].Message.Should().Contain("Serializable");
        AssertHasLocation(findings[0]);
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldReportForbiddenDependencyNamespace()
    {
        using var fixture = await RoslynSolutionFixture.CreateAsync(
            """
            using System.Text;

            namespace SampleProject;

            public sealed class PolicyTarget
            {
                public string Run()
                {
                    return new StringBuilder().Append("Hello").ToString();
                }
            }
            """);

        var rule = CreateRule(
            code: "CP1008",
            title: "No System.Text dependency",
            kind: "dependency_policy",
            severity: RuleSeverity.Error,
            parametersJson: "{\"sourceNamespaces\":[\"SampleProject\"],\"forbiddenNamespaces\":[\"System.Text\"],\"forbiddenTypes\":[]}");

        var findings = await new RoslynRuleAnalyzer().AnalyzeAsync(fixture.SolutionPath, [rule]);

        findings.Should().NotBeEmpty();
        findings.Should().OnlyContain(finding => finding.RuleKind == "dependency_policy");
        findings.Should().Contain(finding => finding.Message.Contains("System.Text", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldReportMethodMetricsViolation()
    {
        using var fixture = await RoslynSolutionFixture.CreateAsync(
            """
            namespace SampleProject;

            public sealed class PolicyTarget
            {
                public int Run(int first, int second, int third)
                {
                    if (first > 0)
                    {
                        return second;
                    }

                    return third;
                }
            }
            """);

        var rule = CreateRule(
            code: "CP1009",
            title: "Small methods only",
            kind: "method_metrics",
            severity: RuleSeverity.Warning,
            parametersJson: "{\"maxLines\":20,\"maxParameters\":2,\"maxCyclomaticComplexity\":10}");

        var findings = await new RoslynRuleAnalyzer().AnalyzeAsync(fixture.SolutionPath, [rule]);

        findings.Should().ContainSingle();
        findings[0].RuleKind.Should().Be("method_metrics");
        findings[0].Message.Should().Contain("parameters exceeds");
        AssertHasLocation(findings[0]);
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldReportExceptionHandlingViolations()
    {
        using var fixture = await RoslynSolutionFixture.CreateAsync(
            """
            using System;

            namespace SampleProject;

            public sealed class PolicyTarget
            {
                public void Run()
                {
                    try
                    {
                        throw new InvalidOperationException();
                    }
                    catch (Exception ex)
                    {
                        throw ex;
                    }
                }
            }
            """);

        var rule = CreateRule(
            code: "CP1010",
            title: "Safe exception handling",
            kind: "exception_handling",
            severity: RuleSeverity.Error,
            parametersJson: "{\"forbidEmptyCatch\":true,\"forbidCatchAll\":true,\"forbidThrowEx\":true,\"requireLogging\":false}");

        var findings = await new RoslynRuleAnalyzer().AnalyzeAsync(fixture.SolutionPath, [rule]);

        findings.Should().HaveCount(2);
        findings.Should().Contain(finding => finding.Message.Contains("catch-all", StringComparison.OrdinalIgnoreCase));
        findings.Should().Contain(finding => finding.Message.Contains("throw ex", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldReportAsyncPolicyViolations()
    {
        using var fixture = await RoslynSolutionFixture.CreateAsync(
            """
            using System.Threading.Tasks;

            namespace SampleProject;

            public sealed class PolicyTarget
            {
                public async void FireAndForget()
                {
                    await Task.Delay(1);
                }

                public async Task Run()
                {
                    var task = Task.Delay(1);
                    task.Wait();
                    await task;
                }
            }
            """);

        var rule = CreateRule(
            code: "CP1011",
            title: "Safe async usage",
            kind: "async_policy",
            severity: RuleSeverity.Warning,
            parametersJson: "{\"forbidAsyncVoid\":true,\"requireCancellationToken\":true,\"forbidBlockingCalls\":true}");

        var findings = await new RoslynRuleAnalyzer().AnalyzeAsync(fixture.SolutionPath, [rule]);

        findings.Should().HaveCount(4);
        findings.Should().Contain(finding => finding.Message.Contains("async void", StringComparison.OrdinalIgnoreCase));
        findings.Should().Contain(finding => finding.Message.Contains("CancellationToken", StringComparison.Ordinal));
        findings.Should().Contain(finding => finding.Message.Contains("blocking async calls", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldReturnNoFindingsForEmptyRuleList()
    {
        var findings = await new RoslynRuleAnalyzer().AnalyzeAsync("/tmp/missing-solution.sln", []);

        findings.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldHonorExcludedFileScopes()
    {
        using var fixture = await RoslynSolutionFixture.CreateAsync(
            """
            namespace SampleProject;

            public sealed class PolicyTarget
            {
                public void Run()
                {
                    var value = 42;
                }
            }
            """);

        var rule = CreateRule(
            code: "CP1004",
            title: "Avoid var outside excluded files",
            kind: "syntax_presence",
            severity: RuleSeverity.Warning,
            parametersJson: "{\"mode\":\"forbid\",\"targets\":[\"local_declaration\"],\"syntaxKinds\":[\"var\"]}",
            scopeJson: "{\"files\":[\"**/*.cs\"],\"excludeFiles\":[\"SampleProject/PolicyTarget.cs\"]}");

        var findings = await new RoslynRuleAnalyzer().AnalyzeAsync(fixture.SolutionPath, [rule]);

        findings.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldThrowClearExceptionForInvalidRuleParameterJson()
    {
        using var fixture = await RoslynSolutionFixture.CreateAsync(
            """
            namespace SampleProject;

            public sealed class PolicyTarget
            {
                public void Run()
                {
                    var value = 42;
                }
            }
            """);

        var rule = CreateRule(
            code: "CP1005",
            title: "Invalid JSON rule",
            kind: "syntax_presence",
            severity: RuleSeverity.Warning,
            parametersJson: "{\"mode\":\"forbid\",\"syntaxKinds\":[\"var\"]");

        var act = async () => await new RoslynRuleAnalyzer().AnalyzeAsync(fixture.SolutionPath, [rule]);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*CP1005*parameters JSON*invalid*");
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldFlowCancellationThroughWorkspaceOperations()
    {
        using var fixture = await RoslynSolutionFixture.CreateAsync(
            """
            namespace SampleProject;

            public sealed class PolicyTarget
            {
                public void Run()
                {
                    var value = 42;
                }
            }
            """);

        var rule = CreateRule(
            code: "CP1006",
            title: "Avoid var with cancellation",
            kind: "syntax_presence",
            severity: RuleSeverity.Warning,
            parametersJson: "{\"mode\":\"forbid\",\"targets\":[\"local_declaration\"],\"syntaxKinds\":[\"var\"]}");
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        var act = async () => await new RoslynRuleAnalyzer().AnalyzeAsync(fixture.SolutionPath, [rule], cancellationTokenSource.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static AuthoredRuleDefinitionDto CreateRule(
        string code,
        string title,
        string kind,
        RuleSeverity severity,
        string parametersJson,
        string scopeJson = "{\"files\":[\"**/*.cs\"],\"excludeFiles\":[]}")
    {
        var now = DateTimeOffset.UtcNow;

        return new AuthoredRuleDefinitionDto(
            Id: Guid.NewGuid(),
            Code: code,
            Title: title,
            Description: null,
            RuleKind: kind,
            SchemaVersion: "1.0",
            Severity: severity,
            ScopeJson: scopeJson,
            ParametersJson: parametersJson,
            RawDefinitionJson: "{}",
            IsEnabled: true,
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
    }

    private static void AssertHasLocation(RuleAnalysisFinding finding)
    {
        finding.StartLine.Should().BeGreaterThan(0);
        finding.StartColumn.Should().BeGreaterThan(0);
        finding.EndLine.Should().BeGreaterThan(0);
        finding.EndColumn.Should().BeGreaterThan(0);
        finding.EndLine.Should().BeGreaterThanOrEqualTo(finding.StartLine);
    }

    private sealed class RoslynSolutionFixture : IDisposable
    {
        private RoslynSolutionFixture(string rootPath, string solutionPath)
        {
            RootPath = rootPath;
            SolutionPath = solutionPath;
        }

        private string RootPath { get; }

        public string SolutionPath { get; }

        public static async Task<RoslynSolutionFixture> CreateAsync(string source)
        {
            var rootPath = Path.Combine(Path.GetTempPath(), "codepass-roslyn-analyzer-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(rootPath);

            RunDotnet(rootPath, "new", "sln", "--name", "SampleSolution");
            RunDotnet(rootPath, "new", "classlib", "--name", "SampleProject", "--framework", "net10.0");

            var sourcePath = Path.Combine(rootPath, "SampleProject", "PolicyTarget.cs");
            await File.WriteAllTextAsync(sourcePath, source);

            RunDotnet(rootPath, "sln", "SampleSolution.sln", "add", Path.Combine("SampleProject", "SampleProject.csproj"));

            return new RoslynSolutionFixture(rootPath, Path.Combine(rootPath, "SampleSolution.sln"));
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(RootPath, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void RunDotnet(string workingDirectory, params string[] arguments)
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = workingDirectory,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            }.WithArguments(arguments));

            process.Should().NotBeNull();
            var stdout = process!.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            process.ExitCode.Should().Be(0, $"dotnet {string.Join(' ', arguments)} failed. stdout: {stdout} stderr: {stderr}");
        }
    }
}

internal static class ProcessStartInfoExtensions
{
    public static ProcessStartInfo WithArguments(this ProcessStartInfo startInfo, params string[] arguments)
    {
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }
}
