using FluentAssertions;
using Xunit;

namespace Meridian.Analyzers.Tests;

public sealed class MER0007ContainRawConfigurationReadsAnalyzerTests
{
    [Fact]
    public async Task ReportsEnvironmentReadOutsideConfigurationBoundaryAsync()
    {
        const string source = """
public sealed class FeatureService
{
    public string? Read()
    {
        return Environment.GetEnvironmentVariable("MERIDIAN_FEATURE_FLAG");
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "apps/backend/Meridian.API/Features/Reports/Services/FeatureService.cs");

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0007ContainRawConfigurationReadsAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportConfigurationLookupInsideOptionsBoundaryAsync()
    {
        const string source = """
public sealed class ReportsOptionsBinder
{
    public string? Bind(IConfiguration configuration)
    {
        return configuration["Reports:QueueName"];
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "apps/backend/Meridian.API/Features/Reports/Options/ReportsOptionsBinder.cs");

        diagnostics.Should().BeEmpty();
    }

    private static async Task<IReadOnlyCollection<Microsoft.CodeAnalysis.Diagnostic>> GetDiagnosticsAsync(
        string source,
        string path)
    {
        return await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0007ContainRawConfigurationReadsAnalyzer(),
            path);
    }
}
