using FluentAssertions;
using Xunit;

namespace Meridian.Analyzer.Tests;

public sealed class MER0016UseSharedJsonProfilesAnalyzerTests
{
    [Fact]
    public async Task ReportsAdHocJsonSerializerOptionsInRuntimeCodeAsync()
    {
        const string source = """
public sealed class DomainDetailMapper
{
    public object Map()
    {
        return new JsonSerializerOptions();
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "src/Api/Features/Domain/DomainDetailMapper.cs");

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0016UseSharedJsonProfilesAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportJsonSerializerOptionsInsideNamedFactoryAsync()
    {
        const string source = """
public sealed class ReportJsonOptionsFactory
{
    public object Create()
    {
        return new JsonSerializerOptions();
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "src/Api/Features/Reports/ReportJsonOptionsFactory.cs");

        diagnostics.Should().BeEmpty();
    }

    private static async Task<IReadOnlyCollection<Microsoft.CodeAnalysis.Diagnostic>> GetDiagnosticsAsync(
        string source,
        string path)
    {
        return await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0016UseSharedJsonProfilesAnalyzer(),
            path);
    }
}
