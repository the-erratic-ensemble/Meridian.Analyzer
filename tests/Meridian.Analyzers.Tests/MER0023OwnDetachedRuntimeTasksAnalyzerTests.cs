using FluentAssertions;
using Xunit;

namespace Meridian.Analyzers.Tests;

public sealed class MER0023OwnDetachedRuntimeTasksAnalyzerTests
{
    [Fact]
    public async Task ReportsTaskRunInRuntimeServiceAsync()
    {
        const string source = """
public sealed class WarmupService
{
    public Task StartAsync()
    {
        return Task.Run(() => WarmAsync());
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0023OwnDetachedRuntimeTasksAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsFireAndForgetAsyncDiscardAsync()
    {
        const string source = """
public sealed class WarmupService
{
    public void Start()
    {
        _ = PublishAsync();
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0023OwnDetachedRuntimeTasksAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportTaskRunInCliBoundaryAsync()
    {
        const string source = """
public sealed class CliCommand
{
    public Task RunAsync()
    {
        return Task.Run(() => Work());
    }
}
""";

        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0023OwnDetachedRuntimeTasksAnalyzer(),
            "apps/backend/Meridian.CLI/Commands/CliCommand.cs");

        diagnostics.Should().BeEmpty();
    }

    private static async Task<IReadOnlyCollection<Microsoft.CodeAnalysis.Diagnostic>> GetDiagnosticsAsync(string source)
    {
        return await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0023OwnDetachedRuntimeTasksAnalyzer(),
            "apps/backend/Meridian.API/Features/Reference/WarmupService.cs");
    }
}
