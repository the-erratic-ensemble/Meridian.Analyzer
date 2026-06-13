using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Meridian.Analyzer.Tests;

public sealed class MER0029AvoidLongLinqChainAnalyzerTests
{
    [Fact]
    public async Task ReportsDiagnostic_ForEightCallEnumerableChainAsync()
    {
        const string source = """
using System.Linq;

public static class Sample
{
    public static int[] Build(int[] values)
    {
        return values
            .Where(value => value > 0)
            .Select(value => value * 2)
            .OrderBy(value => value)
            .ThenBy(value => value.ToString())
            .Skip(1)
            .Take(10)
            .Distinct()
            .ToArray();
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0029AvoidLongLinqChainAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsDiagnostic_ForEightCallQueryableChainWithCustomExtensionsAsync()
    {
        const string source = """
using System.Linq;

public static class QueryExtensions
{
    public static IQueryable<T> TagOne<T>(this IQueryable<T> query) => query;
    public static IQueryable<T> TagTwo<T>(this IQueryable<T> query) => query;
}

public sealed class Entity
{
    public int Id { get; set; }
}

public static class Sample
{
    public static IQueryable<int> Build(IQueryable<Entity> query)
    {
        return query
            .Where(entity => entity.Id > 0)
            .TagOne()
            .Select(entity => entity.Id)
            .OrderBy(id => id)
            .TagTwo()
            .Skip(1)
            .Take(10)
            .Distinct();
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0029AvoidLongLinqChainAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReport_ForSixCallQueryChainAsync()
    {
        const string source = """
using System.Linq;

public static class Sample
{
    public static int[] Build(int[] values)
    {
        return values
            .Where(value => value > 0)
            .Select(value => value * 2)
            .OrderBy(value => value)
            .Skip(1)
            .Take(10)
            .ToArray();
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReport_ForLongNonQueryFluentChainAsync()
    {
        const string source = """
public sealed class Builder
{
    public Builder A() => this;
    public Builder B() => this;
    public Builder C() => this;
    public Builder D() => this;
    public Builder E() => this;
    public Builder F() => this;
    public Builder G() => this;
    public Builder H() => this;
}

public static class Sample
{
    public static Builder Build(Builder builder)
    {
        return builder.A().B().C().D().E().F().G().H();
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ReportsDiagnostic_OnceWhenQueryChainIsFollowedByConfigureAwaitAsync()
    {
        const string source = """
using System.Linq;
using System.Threading.Tasks;

public static class AsyncEnumerableExtensions
{
    public static Task<int[]> ToArrayAsync<T>(this IQueryable<T> query) => Task.FromResult(query.ToArray());
}

public static class Sample
{
    public static ConfiguredTaskAwaitable<int[]> Build(IQueryable<int> query)
    {
        return query
            .Where(value => value > 0)
            .Select(value => value * 2)
            .OrderBy(value => value)
            .ThenBy(value => value.ToString())
            .Skip(1)
            .Take(10)
            .Distinct()
            .ToArrayAsync()
            .ConfigureAwait(false);
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0029AvoidLongLinqChainAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotCountLeadingAsNoTrackingTowardThresholdAsync()
    {
        const string source = """
using System.Linq;

public static class QueryExtensions
{
    public static IQueryable<T> AsNoTracking<T>(this IQueryable<T> query) => query;
}

public sealed class Entity
{
    public int Id { get; set; }
}

public static class Sample
{
    public static int[] Build(IQueryable<Entity> query)
    {
        return query
            .AsNoTracking()
            .Where(entity => entity.Id > 0)
            .Select(entity => entity.Id)
            .OrderBy(id => id)
            .ThenBy(id => id.ToString())
            .Skip(1)
            .Take(10)
            .ToArray();
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source, string path = "src/Infrastructure/Repositories/Repo.cs")
    {
        return await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0029AvoidLongLinqChainAnalyzer(),
            path);
    }
}
