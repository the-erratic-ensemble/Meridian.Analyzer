using FluentAssertions;
using Xunit;

namespace Meridian.Analyzers.Tests;

public sealed class MER0010UseClockAndTimeProviderBoundariesAnalyzerTests
{
    [Fact]
    public async Task ReportsDirectUtcNowInRuntimeCodeAsync()
    {
        const string source = """
public sealed class ReportService
{
    public DateTime Read()
    {
        return DateTime.UtcNow;
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "apps/backend/Meridian.API/Features/Reports/Services/ReportService.cs");

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0010UseClockAndTimeProviderBoundariesAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsRawTaskDelayInRuntimeCodeAsync()
    {
        const string source = """
public sealed class ReportService
{
    public Task WaitAsync()
    {
        return Task.Delay(100);
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "apps/backend/Meridian.API/Features/Reports/Services/ReportService.cs");

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0010UseClockAndTimeProviderBoundariesAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportDirectUtcNowInsideClockBoundaryAsync()
    {
        const string source = """
public sealed class MeridianClock
{
    public DateTime UtcNow()
    {
        return DateTime.UtcNow;
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "apps/backend/Meridian.Shared/Clock/MeridianClock.cs");

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportPassiveEntityUtcNowDefaultAsync()
    {
        const string source = """
public sealed class ReportEntity
{
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "apps/backend/Meridian.Shared/Database/Entities/ReportEntity.cs");

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ReportsEntityMethodUtcNowAsync()
    {
        const string source = """
public sealed class ReportEntity
{
    public DateTime Refresh()
    {
        return DateTime.UtcNow;
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "apps/backend/Meridian.Shared/Database/Entities/ReportEntity.cs");

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0010UseClockAndTimeProviderBoundariesAnalyzer.DiagnosticId);
    }

    private static async Task<IReadOnlyCollection<Microsoft.CodeAnalysis.Diagnostic>> GetDiagnosticsAsync(
        string source,
        string path)
    {
        return await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0010UseClockAndTimeProviderBoundariesAnalyzer(),
            path);
    }
}
