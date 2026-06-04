using FluentAssertions;
using Xunit;

namespace Meridian.Analyzer.Tests;

public sealed class MER0019UseProblemDetailsBoundaryAnalyzerTests
{
    [Fact]
    public async Task ReportsProblemDetailsConstructionInsideControllerActionAsync()
    {
        const string source = """
public sealed class ReportsController : ControllerBase
{
    [HttpGet]
    public object Get()
    {
        return new ProblemDetails();
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0019UseProblemDetailsBoundaryAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportProblemDetailsConstructionInsideFactoryAsync()
    {
        const string source = """
public sealed class ReportProblemDetailsFactory
{
    public object Create()
    {
        return new ProblemDetails();
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
            new MER0019UseProblemDetailsBoundaryAnalyzer());
    }
}
