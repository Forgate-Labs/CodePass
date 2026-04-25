using CodePass.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodePass.Web.Data;

public sealed class CodePassDbContext(DbContextOptions<CodePassDbContext> options) : DbContext(options)
{
    public DbSet<RegisteredSolution> RegisteredSolutions => Set<RegisteredSolution>();
    public DbSet<AuthoredRuleDefinition> AuthoredRuleDefinitions => Set<AuthoredRuleDefinition>();
    public DbSet<SolutionRuleAssignment> SolutionRuleAssignments => Set<SolutionRuleAssignment>();
    public DbSet<RuleAnalysisRun> RuleAnalysisRuns => Set<RuleAnalysisRun>();
    public DbSet<RuleAnalysisViolation> RuleAnalysisViolations => Set<RuleAnalysisViolation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var registeredSolution = modelBuilder.Entity<RegisteredSolution>();

        registeredSolution.HasKey(solution => solution.Id);
        registeredSolution.Property(solution => solution.DisplayName).IsRequired();
        registeredSolution.Property(solution => solution.SolutionPath).IsRequired();
        registeredSolution.Property(solution => solution.Status).HasConversion<string>().IsRequired();
        registeredSolution.HasIndex(solution => solution.DisplayName);
        registeredSolution.HasIndex(solution => solution.SolutionPath).IsUnique();

        var authoredRuleDefinition = modelBuilder.Entity<AuthoredRuleDefinition>();

        authoredRuleDefinition.HasKey(rule => rule.Id);
        authoredRuleDefinition.Property(rule => rule.Code).IsRequired();
        authoredRuleDefinition.Property(rule => rule.Title).IsRequired();
        authoredRuleDefinition.Property(rule => rule.Description);
        authoredRuleDefinition.Property(rule => rule.RuleKind).IsRequired();
        authoredRuleDefinition.Property(rule => rule.SchemaVersion).IsRequired();
        authoredRuleDefinition.Property(rule => rule.Severity).HasConversion<string>().IsRequired();
        authoredRuleDefinition.Property(rule => rule.ScopeJson).IsRequired();
        authoredRuleDefinition.Property(rule => rule.ParametersJson).IsRequired();
        authoredRuleDefinition.Property(rule => rule.RawDefinitionJson).IsRequired();
        authoredRuleDefinition.Property(rule => rule.IsEnabled).IsRequired();
        authoredRuleDefinition.HasIndex(rule => rule.Code).IsUnique();
        authoredRuleDefinition.HasIndex(rule => rule.RuleKind);

        var solutionRuleAssignment = modelBuilder.Entity<SolutionRuleAssignment>();

        solutionRuleAssignment.HasKey(assignment => assignment.Id);
        solutionRuleAssignment.Property(assignment => assignment.IsEnabled).IsRequired();
        solutionRuleAssignment.HasOne<RegisteredSolution>()
            .WithMany()
            .HasForeignKey(assignment => assignment.RegisteredSolutionId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        solutionRuleAssignment.HasOne<AuthoredRuleDefinition>()
            .WithMany()
            .HasForeignKey(assignment => assignment.AuthoredRuleDefinitionId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        solutionRuleAssignment.HasIndex(assignment => new { assignment.RegisteredSolutionId, assignment.AuthoredRuleDefinitionId })
            .IsUnique();

        var ruleAnalysisRun = modelBuilder.Entity<RuleAnalysisRun>();

        ruleAnalysisRun.HasKey(run => run.Id);
        ruleAnalysisRun.Property(run => run.Status).HasConversion<string>().IsRequired();
        ruleAnalysisRun.Property(run => run.RuleCount).IsRequired();
        ruleAnalysisRun.Property(run => run.TotalViolations).IsRequired();
        ruleAnalysisRun.Property(run => run.ErrorMessage);
        ruleAnalysisRun.HasOne<RegisteredSolution>()
            .WithMany()
            .HasForeignKey(run => run.RegisteredSolutionId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        ruleAnalysisRun.HasMany(run => run.Violations)
            .WithOne(violation => violation.RuleAnalysisRun)
            .HasForeignKey(violation => violation.RuleAnalysisRunId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        ruleAnalysisRun.HasIndex(run => run.RegisteredSolutionId);
        ruleAnalysisRun.HasIndex(run => run.StartedAtUtc);

        var ruleAnalysisViolation = modelBuilder.Entity<RuleAnalysisViolation>();

        ruleAnalysisViolation.HasKey(violation => violation.Id);
        ruleAnalysisViolation.Property(violation => violation.RuleCode).IsRequired();
        ruleAnalysisViolation.Property(violation => violation.RuleTitle).IsRequired();
        ruleAnalysisViolation.Property(violation => violation.RuleKind).IsRequired();
        ruleAnalysisViolation.Property(violation => violation.RuleSeverity).HasConversion<string>().IsRequired();
        ruleAnalysisViolation.Property(violation => violation.Message).IsRequired();
        ruleAnalysisViolation.Property(violation => violation.FilePath).IsRequired();
        ruleAnalysisViolation.HasOne<AuthoredRuleDefinition>()
            .WithMany()
            .HasForeignKey(violation => violation.AuthoredRuleDefinitionId)
            .OnDelete(DeleteBehavior.SetNull);
        ruleAnalysisViolation.HasIndex(violation => violation.RuleAnalysisRunId);
        ruleAnalysisViolation.HasIndex(violation => violation.RuleCode);
    }
}
