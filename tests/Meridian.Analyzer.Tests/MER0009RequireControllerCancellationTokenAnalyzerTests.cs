using FluentAssertions;
using Xunit;

namespace Meridian.Analyzer.Tests;

public sealed class MER0009RequireControllerCancellationTokenAnalyzerTests
{
    [Fact]
    public async Task ReportsAsyncControllerActionWithoutCancellationTokenAsync()
    {
        const string source = """
public sealed class ReportsController : ControllerBase
{
    [HttpGet]
    public async Task<object> GetAsync()
    {
        return await LoadAsync();
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0009RequireControllerCancellationTokenAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsCancellationTokenNoneInsideControllerActionAsync()
    {
        const string source = """
public sealed class ReportsController : ControllerBase
{
    [HttpGet]
    public Task<object> GetAsync(CancellationToken cancellationToken)
    {
        return LoadAsync(CancellationToken.None);
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0009RequireControllerCancellationTokenAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportAsyncControllerActionWithCancellationTokenAsync()
    {
        const string source = """
public sealed class ReportsController : ControllerBase
{
    [HttpGet]
    public Task<object> GetAsync(CancellationToken cancellationToken)
    {
        return LoadAsync(cancellationToken);
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
            new MER0009RequireControllerCancellationTokenAnalyzer());
    }
}
