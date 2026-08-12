using FluentAssertions;
using Xunit;

namespace Meridian.Analyzer.Tests;

public sealed class MER0034PreferStringComparisonExtensionsAnalyzerTests
{
    [Fact]
    public async Task ReportsDiagnostic_ForStaticOrdinalStringEqualsAsync()
    {
        const string source = """
using System;

public sealed class Sample
{
    public bool IsMatch(string? value, string? other)
    {
        return string.Equals(value, other, StringComparison.Ordinal);
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Id == MER0034PreferStringComparisonExtensionsAnalyzer.DiagnosticId &&
            diagnostic.GetMessage().Contains("EqualsOrdinal"));
    }

    [Fact]
    public async Task ReportsDiagnostic_ForStaticOrdinalIgnoreCaseStringEqualsAsync()
    {
        const string source = """
using System;

public sealed class Sample
{
    public bool IsMatch(string? value, string? other)
    {
        return string.Equals(value, other, StringComparison.OrdinalIgnoreCase);
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Id == MER0034PreferStringComparisonExtensionsAnalyzer.DiagnosticId &&
            diagnostic.GetMessage().Contains("EqualsOrdinalIgnoreCase"));
    }

    [Fact]
    public async Task ReportsDiagnostic_ForInstanceOrdinalStringEqualsAsync()
    {
        const string source = """
using System;

public sealed class Sample
{
    public bool IsMatch(string value, string other)
    {
        return value.Equals(other, StringComparison.Ordinal);
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Id == MER0034PreferStringComparisonExtensionsAnalyzer.DiagnosticId &&
            diagnostic.GetMessage().Contains("EqualsOrdinal"));
    }

    [Fact]
    public async Task ReportsDiagnostic_ForOrdinalStringContainsAsync()
    {
        const string source = """
using System;

public sealed class Sample
{
    public bool HasToken(string value, string token)
    {
        return value.Contains(token, StringComparison.Ordinal);
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Id == MER0034PreferStringComparisonExtensionsAnalyzer.DiagnosticId &&
            diagnostic.GetMessage().Contains("ContainsOrdinal"));
    }

    [Fact]
    public async Task ReportsDiagnostic_ForOrdinalIgnoreCaseStringContainsAsync()
    {
        const string source = """
using System;

public sealed class Sample
{
    public bool HasToken(string value, string token)
    {
        return value.Contains(token, StringComparison.OrdinalIgnoreCase);
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Id == MER0034PreferStringComparisonExtensionsAnalyzer.DiagnosticId &&
            diagnostic.GetMessage().Contains("ContainsOrdinalIgnoreCase"));
    }

    [Fact]
    public async Task DoesNotReport_ForStartsWithBecauseNoSharedHelperExistsAsync()
    {
        const string source = """
using System;

public sealed class Sample
{
    public bool HasPrefix(string value, string prefix)
    {
        return value.StartsWith(prefix, StringComparison.Ordinal);
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReport_ForCultureComparisonAsync()
    {
        const string source = """
using System;

public sealed class Sample
{
    public bool IsMatch(string? value, string? other)
    {
        return string.Equals(value, other, StringComparison.CurrentCultureIgnoreCase);
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReport_InsideQueryablePredicateAsync()
    {
        const string source = """
using System;
using System.Linq;

public sealed class Entity
{
    public string? Name { get; init; }
}

public sealed class Repo
{
    public IQueryable<Entity> Filter(IQueryable<Entity> query)
    {
        return query.Where(entity => string.Equals(entity.Name, "active", StringComparison.Ordinal));
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReport_InsideExpressionPredicateAsync()
    {
        const string source = """
using System;
using System.Linq.Expressions;

public sealed class Entity
{
    public string? Name { get; init; }
}

public sealed class Repo
{
    public Expression<Func<Entity, bool>> BuildPredicate()
    {
        return entity => string.Equals(entity.Name, "active", StringComparison.Ordinal);
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReport_ForAssertionContainsOverloadAsync()
    {
        const string source = """
using System;

public static class Assert
{
    public static void Contains(string expected, string actual, StringComparison comparisonType) {}
}

public sealed class SampleTests
{
    public void Test(string diagnostic)
    {
        Assert.Contains("expected", diagnostic, StringComparison.Ordinal);
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportInsideStringExtensionsImplementationAsync()
    {
        const string source = """
using System;

public static class StringExtensions
{
    public static bool EqualsOrdinal(this string? value, string? other)
    {
        return string.Equals(value, other, StringComparison.Ordinal);
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(
            source,
            "apps/backend/Foundry.Shared/Extensions/StringExtensions.cs");

        diagnostics.Should().BeEmpty();
    }

    private static async Task<IReadOnlyCollection<Microsoft.CodeAnalysis.Diagnostic>> GetDiagnosticsAsync(
        string source,
        string path = "apps/backend/Foundry.Application/Sample.cs")
    {
        return await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0034PreferStringComparisonExtensionsAnalyzer(),
            path);
    }
}
