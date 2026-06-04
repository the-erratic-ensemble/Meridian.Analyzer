using FluentAssertions;
using Xunit;

namespace Meridian.Analyzer.Tests;

public sealed class MER0021EnforceBackendLoggingContractAnalyzerTests
{
    [Fact]
    public async Task ReportsMicrosoftLoggerInRuntimeServiceAsync()
    {
        const string source = """
public sealed class ReferenceDataService
{
    public ReferenceDataService(ILogger<ReferenceDataService> logger)
    {
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "src/Api/Features/Reference/ReferenceDataService.cs");

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0021EnforceBackendLoggingContractAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsConsoleWriteInRuntimeServiceAsync()
    {
        const string source = """
public sealed class StartupPipelineConfigurator
{
    public void Configure()
    {
        Console.Error.WriteLine("failed");
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "src/Api/StartupPipelineConfigurator.cs");

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0021EnforceBackendLoggingContractAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportMicrosoftLoggerInsideMiddlewareBoundaryAsync()
    {
        const string source = """
public sealed class RequestLoggingMiddleware
{
    public RequestLoggingMiddleware(ILogger<RequestLoggingMiddleware> logger)
    {
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "src/Api/Middleware/RequestLoggingMiddleware.cs");

        diagnostics.Should().BeEmpty();
    }

    private static async Task<IReadOnlyCollection<Microsoft.CodeAnalysis.Diagnostic>> GetDiagnosticsAsync(
        string source,
        string path)
    {
        return await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0021EnforceBackendLoggingContractAnalyzer(),
            path);
    }
}
