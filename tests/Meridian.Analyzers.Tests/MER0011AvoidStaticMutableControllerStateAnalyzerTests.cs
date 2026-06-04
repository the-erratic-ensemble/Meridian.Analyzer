using FluentAssertions;
using Xunit;

namespace Meridian.Analyzers.Tests;

public sealed class MER0011AvoidStaticMutableControllerStateAnalyzerTests
{
    [Fact]
    public async Task ReportsStaticMutableCollectionInControllerAsync()
    {
        const string source = """
public sealed class ReportsController : ControllerBase
{
    private static readonly ConcurrentDictionary<string, string> Windows = new();
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0011AvoidStaticMutableControllerStateAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportInstanceCollectionInControllerAsync()
    {
        const string source = """
public sealed class ReportsController : ControllerBase
{
    private readonly ConcurrentDictionary<string, string> _windows = new();
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    private static async Task<IReadOnlyCollection<Microsoft.CodeAnalysis.Diagnostic>> GetDiagnosticsAsync(string source)
    {
        return await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0011AvoidStaticMutableControllerStateAnalyzer());
    }
}
