using CodePass.Web.Data.Entities;
using CodePass.Web.Services.Solutions;
using FluentAssertions;

namespace CodePass.Web.Tests.Services;

public sealed class SolutionPathValidatorTests
{
    private readonly SolutionPathValidator _validator = new();

    [Fact]
    public async Task ValidateAsync_ShouldAcceptExistingSolutionFile()
    {
        using var tempDirectory = new TemporaryDirectory();
        var solutionPath = tempDirectory.CreateFile("CodePass.sln");

        var result = await _validator.ValidateAsync(solutionPath);

        result.Status.Should().Be(RegisteredSolutionStatus.Valid);
        result.CanonicalPath.Should().Be(Path.GetFullPath(solutionPath));
    }

    [Fact]
    public async Task ValidateAsync_ShouldRejectWrongExtensionAsInvalid()
    {
        using var tempDirectory = new TemporaryDirectory();
        var solutionPath = tempDirectory.CreateFile("CodePass.csproj");

        var result = await _validator.ValidateAsync(solutionPath);

        result.Status.Should().Be(RegisteredSolutionStatus.Invalid);
    }

    [Fact]
    public async Task ValidateAsync_ShouldRejectMissingSolutionFile()
    {
        using var tempDirectory = new TemporaryDirectory();
        var solutionPath = Path.Combine(tempDirectory.Path, "Missing.sln");

        var result = await _validator.ValidateAsync(solutionPath);

        result.Status.Should().Be(RegisteredSolutionStatus.FileNotFound);
    }

    [Fact]
    public async Task ValidateAsync_ShouldRejectInaccessibleSolutionFile()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        using var tempDirectory = new TemporaryDirectory();
        var solutionPath = tempDirectory.CreateFile("Restricted.sln");
        File.SetUnixFileMode(solutionPath, UnixFileMode.None);

        try
        {
            var result = await _validator.ValidateAsync(solutionPath);
            result.Status.Should().Be(RegisteredSolutionStatus.PathInaccessible);
        }
        finally
        {
            File.SetUnixFileMode(solutionPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}

internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"codepass-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string CreateFile(string relativePath, string content = "Microsoft Visual Studio Solution File, Format Version 12.00")
    {
        var fullPath = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
