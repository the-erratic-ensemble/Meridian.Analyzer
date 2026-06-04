using FluentAssertions;
using Xunit;

namespace Meridian.Analyzers.Tests;

public sealed class MER0018ContainRawSqlAnalyzerTests
{
    [Fact]
    public async Task ReportsRawSqlApiAsync()
    {
        const string source = """
public sealed class ReportRepository
{
    public object Read(dynamic db)
    {
        return db.Reports.FromSqlRaw("select * from reports");
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "apps/backend/Meridian.Infrastructure/Repositories/ReportRepository.cs");

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0018ContainRawSqlAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportInterpolatedSqlInsideRepositoryBoundaryAsync()
    {
        const string source = """
public sealed class ReportRepository
{
    public object Read(dynamic db, int id)
    {
        return db.Reports.FromSqlInterpolated($"select * from reports where id = {id}");
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "apps/backend/Meridian.Infrastructure/Repositories/ReportRepository.cs");

        diagnostics.Should().BeEmpty();
    }

    private static async Task<IReadOnlyCollection<Microsoft.CodeAnalysis.Diagnostic>> GetDiagnosticsAsync(
        string source,
        string path)
    {
        return await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0018ContainRawSqlAnalyzer(),
            path);
    }
}
