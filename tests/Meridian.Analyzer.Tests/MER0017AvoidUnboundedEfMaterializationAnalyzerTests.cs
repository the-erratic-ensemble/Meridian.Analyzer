using FluentAssertions;
using Xunit;

namespace Meridian.Analyzer.Tests;

public sealed class MER0017AvoidUnboundedEfMaterializationAnalyzerTests
{
    [Fact]
    public async Task ReportsUnboundedAsyncMaterializationAsync()
    {
        const string source = """
public sealed class ReportRepository
{
    public Task<object> GetAsync(dynamic reports)
    {
        return reports.ToListAsync();
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0017AvoidUnboundedEfMaterializationAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportBoundedAsyncMaterializationAsync()
    {
        const string source = """
public sealed class ReportRepository
{
    public Task<object> GetAsync(dynamic reports)
    {
        return reports.Where(report => report.IsActive).Take(100).ToListAsync();
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportLocallyComposedBoundedMaterializationAsync()
    {
        const string source = """
public sealed class ReportRepository
{
    public Task<object> GetAsync(dynamic reports)
    {
        var boundedReports = reports.Where(report => report.IsActive).Take(100);
        return boundedReports.ToListAsync();
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportProjectionFromBoundedLocalAsync()
    {
        const string source = """
public sealed class ReportRepository
{
    public Task<object> GetAsync(dynamic reports)
    {
        var boundedReports = reports.Where(report => report.IsActive);

        return boundedReports
            .Select(report => report.Id)
            .ToListAsync();
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportQuerySyntaxWhereMaterializationAsync()
    {
        const string source = """
public sealed class ReportRepository
{
    public Task<object> GetAsync(dynamic links, dynamic areas)
    {
        var query =
            from link in links
            join area in areas on link.AreaId equals area.Id
            where link.PostcodeId == 42
            select area;

        return query.ToListAsync();
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportBoundedHelperQueryMaterializationAsync()
    {
        const string source = """
public sealed class ReportRepository
{
    public Task<object> GetAsync(dynamic reports)
    {
        return BuildQuery(reports).ToListAsync();
    }

    private dynamic BuildQuery(dynamic reports)
    {
        return reports.Where(report => report.IsActive).Take(100);
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportBoundedLocalReassignmentAsync()
    {
        const string source = """
public sealed class ReportRepository
{
    public Task<object> GetAsync(dynamic reports)
    {
        var query = reports.AsQueryable();
        query = query.Where(report => report.IsActive).Take(100);

        return query.ToListAsync();
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportRawSqlWithEmbeddedBoundAsync()
    {
        const string source = """"
public sealed class ReportRepository
{
    public Task<object> GetAsync(dynamic database)
    {
        return database
            .SqlQueryRaw<object>("""
                SELECT *
                FROM reports
                WHERE tenant_id = @tenant_id
                LIMIT @p_limit
                """)
            .ToListAsync();
    }
}
"""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportAggregateMaterializationAsync()
    {
        const string source = """
public sealed class ReportRepository
{
    public Task<object> GetAsync(dynamic reports)
    {
        return reports
            .GroupBy(report => report.Status)
            .ToDictionaryAsync(group => group.Key, group => group.Count());
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportIntentionalFullMaterializationBoundaryAsync()
    {
        const string source = """
public sealed class ReportRepository
{
    public Task<object> GetAllAsync(dynamic reports)
    {
        return reports.ToListAsync();
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportIntentionalMaintenanceBoundaryAsync()
    {
        const string source = """
public sealed class ReportRepository
{
    public Task<object> ValidateAndRepairHierarchyAsync(dynamic localities)
    {
        return localities.ToListAsync();
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    private static async Task<IReadOnlyCollection<Microsoft.CodeAnalysis.Diagnostic>> GetDiagnosticsAsync(string source)
    {
        return await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0017AvoidUnboundedEfMaterializationAnalyzer(),
            "src/Infrastructure/Repositories/ReportRepository.cs");
    }
}
