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
