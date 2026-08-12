using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Meridian.Analyzer.Tests;

public sealed class MER0033AvoidHeavyInlineQueryLambdasAnalyzerTests
{
    [Fact]
    public async Task ReportsDiagnostic_ForCareerAdvancementCandidateSelectionShapeAsync()
    {
        const string source = """
using System;
using System.Collections.Generic;
using System.Linq;

public sealed record Club(string Id);
public sealed record Vacancy(string Id, string ClubId, string Status, DateTime OpenedAt);
public sealed record Manager(string Id, string ControlSource, string AvailabilityStatus, string FutureRoleEligibility);
public sealed record Evaluation(bool IsOffered, int CandidateScore);
public sealed record Candidate(Manager Manager, Vacancy Vacancy, Evaluation Evaluation);

public static class Policy
{
    public static Evaluation Evaluate(Manager manager, Club club, Vacancy vacancy) => new(true, manager.Id.Length);
}

public static class Sample
{
    public static Candidate? Pick(
        IReadOnlyCollection<Vacancy> vacancies,
        IReadOnlyCollection<Manager> managers,
        Dictionary<string, Club> clubs,
        HashSet<string> occupiedClubIds,
        HashSet<string> reservedClubIds,
        HashSet<string> employedManagerIds)
    {
        return vacancies
            .Where(vacancy => vacancy.Status == "open"
                && !occupiedClubIds.Contains(vacancy.ClubId)
                && !reservedClubIds.Contains(vacancy.ClubId)
                && clubs.ContainsKey(vacancy.ClubId))
            .SelectMany(vacancy => managers
                .Where(manager => manager.ControlSource == "ai"
                    && !employedManagerIds.Contains(manager.Id)
                    && string.Equals(manager.AvailabilityStatus, "available", StringComparison.Ordinal)
                    && string.Equals(manager.FutureRoleEligibility, "eligible", StringComparison.Ordinal))
                .Select(manager => {
                    Evaluation evaluation = Policy.Evaluate(
                        manager,
                        clubs[vacancy.ClubId],
                        vacancy);
                    return new Candidate(manager, vacancy, evaluation);
                }))
            .Where(candidate => candidate.Evaluation.IsOffered)
            .OrderByDescending(candidate => candidate.Evaluation.CandidateScore)
            .ThenBy(candidate => candidate.Vacancy.OpenedAt)
            .ThenBy(candidate => candidate.Vacancy.Id, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Manager.Id, StringComparer.Ordinal)
            .FirstOrDefault();
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0033AvoidHeavyInlineQueryLambdasAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsDiagnostic_ForNestedQueryPipelineInsideSelectManyLambdaAsync()
    {
        const string source = """
using System;
using System.Collections.Generic;
using System.Linq;

public sealed record Vacancy(string Id, string ClubId);
public sealed record Manager(string Id, string Status);
public sealed record Candidate(Manager Manager, Vacancy Vacancy, int Score);

public static class Sample
{
    public static Candidate? Pick(
        IReadOnlyCollection<Vacancy> vacancies,
        IReadOnlyCollection<Manager> managers)
    {
        return vacancies
            .Where(vacancy => vacancy.ClubId.Length > 0)
            .SelectMany(vacancy => managers
                .Where(manager => manager.Id.Length > 0
                    && string.Equals(manager.Status, "available", StringComparison.Ordinal))
                .Select(manager => {
                    var score = manager.Id.Length + vacancy.Id.Length;
                    return new Candidate(manager, vacancy, score);
                }))
            .FirstOrDefault();
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0033AvoidHeavyInlineQueryLambdasAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsDiagnostic_ForMultiStatementValueConstructionLambdaAsync()
    {
        const string source = """
using System.Collections.Generic;
using System.Linq;

public sealed record Projection(int Id, string Label);

public static class Sample
{
    public static Projection[] Build(IReadOnlyCollection<int> values)
    {
        return values
            .Select(value => {
                var id = value + 1;
                var label = id.ToString();
                return new Projection(
                    id,
                    label);
            })
            .ToArray();
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0033AvoidHeavyInlineQueryLambdasAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsDiagnostic_ForMultiStatementFactoryReturnLambdaAsync()
    {
        const string source = """
using System.Collections.Generic;
using System.Linq;

public sealed record Projection(int Id, string Label);

public static class ProjectionFactory
{
    public static Projection Create(int id, string label) => new(id, label);
}

public static class Sample
{
    public static Projection[] Build(IReadOnlyCollection<int> values)
    {
        return values
            .Select(value => {
                var id = value + 1;
                var label = id.ToString();
                return ProjectionFactory.Create(
                    id,
                    label);
            })
            .ToArray();
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0033AvoidHeavyInlineQueryLambdasAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReport_ForShortExpressionLambdaAsync()
    {
        const string source = """
using System.Collections.Generic;
using System.Linq;

public sealed record Projection(int Id);

public static class Sample
{
    public static Projection[] Build(IReadOnlyCollection<int> values)
    {
        return values
            .Select(value => new Projection(value))
            .ToArray();
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReport_ForShortStatementLambdaAsync()
    {
        const string source = """
using System.Collections.Generic;
using System.Linq;

public static class Sample
{
    public static int[] Build(IReadOnlyCollection<int> values)
    {
        return values.Select(value => {
            var adjusted = value + 1;
            return adjusted;
        }).ToArray();
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReport_ForNonQueryFluentSelectLambdaAsync()
    {
        const string source = """
public sealed class Builder
{
    public Builder Select(System.Func<int, int> selector) => this;
}

public static class Sample
{
    public static Builder Build(Builder builder)
    {
        return builder.Select(value => {
            var first = value + 1;
            var second = first + 1;
            return second;
        });
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source, string path = "src/Application/Sample.cs")
    {
        return await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0033AvoidHeavyInlineQueryLambdasAnalyzer(),
            path);
    }
}
