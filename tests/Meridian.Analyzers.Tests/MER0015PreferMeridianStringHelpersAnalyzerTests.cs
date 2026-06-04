using FluentAssertions;
using Xunit;

namespace Meridian.Analyzers.Tests;

public sealed class MER0015PreferMeridianStringHelpersAnalyzerTests
{
    [Fact]
    public async Task ReportsRawNullOrWhitespaceCheckInRuntimeCodeAsync()
    {
        const string source = """
public sealed class ReportService
{
    public bool HasName(string value)
    {
        return !string.IsNullOrWhiteSpace(value);
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "apps/backend/Meridian.API/Features/Reports/Services/ReportService.cs");

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0015PreferMeridianStringHelpersAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportRawNullOrWhitespaceCheckInsideQueryExpressionAsync()
    {
        const string source = """
using System.Linq;

public sealed class Report
{
    public string? Name { get; init; }
}

public sealed class ReportRepository
{
    public IQueryable<Report> Query(IQueryable<Report> reports)
    {
        return reports.Where(report => !string.IsNullOrWhiteSpace(report.Name));
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "apps/backend/Meridian.Infrastructure/Repositories/ReportRepository.cs");

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportRawNullOrWhitespaceCheckInsideExpressionPredicateAsync()
    {
        const string source = """
using System;
using System.Linq.Expressions;

public sealed class Report
{
    public string? Name { get; init; }
}

public sealed class ReportRepository
{
    public Expression<Func<Report, bool>> BuildPredicate()
    {
        return report => !string.IsNullOrWhiteSpace(report.Name);
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "apps/backend/Meridian.Infrastructure/Repositories/ReportRepository.cs");

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportRawNullOrWhitespaceCheckInsideQueryableQuerySyntaxAsync()
    {
        const string source = """
using System.Linq;

public sealed class Report
{
    public string? Name { get; init; }
}

public sealed class ReportRepository
{
    public IQueryable<Report> Query(IQueryable<Report> reports)
    {
        return from report in reports
               where !string.IsNullOrWhiteSpace(report.Name)
               select report;
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "apps/backend/Meridian.Infrastructure/Repositories/ReportRepository.cs");

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ReportsRawNullOrWhitespaceCheckInsideEnumerableLambdaAsync()
    {
        const string source = """
using System.Collections.Generic;
using System.Linq;

public sealed class Report
{
    public string? Name { get; init; }
}

public sealed class ReportService
{
    public IEnumerable<Report> Filter(IEnumerable<Report> reports)
    {
        return reports.Where(report => !string.IsNullOrWhiteSpace(report.Name));
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "apps/backend/Meridian.API/Features/Reports/Services/ReportService.cs");

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0015PreferMeridianStringHelpersAnalyzer.DiagnosticId);
    }

    private static async Task<IReadOnlyCollection<Microsoft.CodeAnalysis.Diagnostic>> GetDiagnosticsAsync(
        string source,
        string path)
    {
        return await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0015PreferMeridianStringHelpersAnalyzer(),
            path);
    }
}
