using FluentAssertions;
using Xunit;

namespace Meridian.Analyzer.Tests;

public sealed class MER0008ContainStartupBypassFlagsAnalyzerTests
{
    [Fact]
    public async Task ReportsEnvironmentReadForStartupBypassFlagOutsideApprovedBoundaryAsync()
    {
        const string source = """
public sealed class EmailRegistration
{
    public bool IsSkipped()
    {
        return Environment.GetEnvironmentVariable("MERIDIAN_SKIP_EMAIL") == "true";
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "src/Api/Features/Communications/EmailRegistration.cs");

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0008ContainStartupBypassFlagsAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsConfigurationGetValueForStartupBypassFlagOutsideApprovedBoundaryAsync()
    {
        const string source = """
public sealed class ReportRegistration
{
    public bool IsSkipped(IConfiguration configuration)
    {
        return configuration.GetValue<bool>("MERIDIAN_SKIP_BACKGROUND_SERVICES");
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "src/Api/Features/Reports/ReportsServiceRegistrationExtensions.cs");

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0008ContainStartupBypassFlagsAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsConfigurationIndexerForStartupBypassFlagOutsideApprovedBoundaryAsync()
    {
        const string source = """
public sealed class ReportRegistration
{
    public string? Read(IConfiguration configuration)
    {
        return configuration["MERIDIAN_SKIP_REDIS"];
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "src/Api/Features/Reports/ReportsServiceRegistrationExtensions.cs");

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0008ContainStartupBypassFlagsAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsConstStartupBypassFlagOutsideApprovedBoundaryAsync()
    {
        const string source = """
public sealed class ReportRegistration
{
    private const string BackgroundServicesSkipKey = "MERIDIAN_SKIP_BACKGROUND_SERVICES";

    public bool IsSkipped(IConfiguration configuration)
    {
        return configuration.GetValue<bool>(BackgroundServicesSkipKey);
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "src/Api/Features/Reports/ReportsServiceRegistrationExtensions.cs");

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0008ContainStartupBypassFlagsAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportStartupBypassFlagInsideStartupGuardsAsync()
    {
        const string source = """
public static class StartupGuards
{
    public static bool IsSkipped(IConfiguration configuration)
    {
        return configuration.GetValue<bool>("MERIDIAN_SKIP_REDIS");
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "src/Api/Infrastructure/Startup/StartupGuards.cs");

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportNonBypassEnvironmentReadAsync()
    {
        const string source = """
public sealed class EnvironmentReader
{
    public string? GetEnvironment()
    {
        return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "src/Api/Infrastructure/Startup/StartupEnvironmentLoader.cs");

        diagnostics.Should().BeEmpty();
    }

    private static async Task<IReadOnlyCollection<Microsoft.CodeAnalysis.Diagnostic>> GetDiagnosticsAsync(
        string source,
        string path)
    {
        return await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0008ContainStartupBypassFlagsAnalyzer(),
            path);
    }
}
