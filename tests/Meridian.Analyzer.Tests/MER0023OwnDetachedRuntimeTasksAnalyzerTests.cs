using FluentAssertions;
using Xunit;

namespace Meridian.Analyzer.Tests;

public sealed class MER0023OwnDetachedRuntimeTasksAnalyzerTests
{
    [Fact]
    public async Task ReportsDiscardedTaskRunAsync()
    {
        const string source = """
using System.Threading.Tasks;

public sealed class WarmupService
{
    public void Start()
    {
        _ = Task.Run(() => Work());
    }

    private static void Work() { }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0023OwnDetachedRuntimeTasksAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsUnobservedTaskRunLocalAsync()
    {
        const string source = """
using System.Threading.Tasks;

public sealed class WarmupService
{
    public void Start()
    {
        var task = Task.Run(() => Work());
    }

    private static void Work() { }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0023OwnDetachedRuntimeTasksAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsFireAndForgetTaskDiscardAsync()
    {
        const string source = """
using System.Threading.Tasks;

public sealed class WarmupService
{
    public void Start()
    {
        _ = PublishAsync();
    }

    private static Task PublishAsync() => Task.CompletedTask;
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0023OwnDetachedRuntimeTasksAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportReturnedTaskRunAsync()
    {
        const string source = """
using System.Threading.Tasks;

public sealed class WarmupService
{
    public Task StartAsync()
    {
        return Task.Run(() => Work());
    }

    private static void Work() { }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportAwaitedTaskRunAsync()
    {
        const string source = """
using System.Threading.Tasks;

public sealed class WarmupService
{
    public async Task StartAsync()
    {
        await Task.Run(() => Work());
    }

    private static void Work() { }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportTaskRunCollectionAwaitedByWhenAllAsync()
    {
        const string source = """
using System.Linq;
using System.Threading.Tasks;

public sealed class WarmupService
{
    public async Task StartAsync()
    {
        var tasks = new[] { 1, 2 }
            .Select(value => Task.Run(() => Work(value)))
            .ToArray();
        await Task.WhenAll(tasks);
    }

    private static void Work(int value) { }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportTaskRunOwnedByBackgroundTaskOwnerAsync()
    {
        const string source = """
using System.Threading.Tasks;

public interface IBackgroundTaskOwner
{
    void Track(Task task);
}

public sealed class WarmupService
{
    private readonly IBackgroundTaskOwner _taskOwner;

    public WarmupService(IBackgroundTaskOwner taskOwner)
    {
        _taskOwner = taskOwner;
    }

    public void Start()
    {
        _taskOwner.Track(Task.Run(() => Work()));
    }

    private static void Work() { }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportCancellationTokenNoneAsync()
    {
        const string source = """
using System.Threading;
using System.Threading.Tasks;

public sealed class WarmupService
{
    public Task StartAsync()
    {
        return WorkAsync(CancellationToken.None);
    }

    private static Task WorkAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ReportsDetachedTaskInCliBoundaryAsync()
    {
        const string source = """
using System.Threading.Tasks;

public sealed class CliCommand
{
    public void Run()
    {
        _ = Task.Run(() => Work());
    }

    private static void Work() { }
}
""";

        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0023OwnDetachedRuntimeTasksAnalyzer(),
            "src/Cli/Commands/CliCommand.cs");

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0023OwnDetachedRuntimeTasksAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportDiscardedVoidMethodNamedAsync()
    {
        const string source = """
public sealed class WarmupService
{
    public void Start()
    {
        _ = PublishAsync();
    }

    private static int PublishAsync() => 1;
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    private static async Task<IReadOnlyCollection<Microsoft.CodeAnalysis.Diagnostic>> GetDiagnosticsAsync(string source)
    {
        return await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0023OwnDetachedRuntimeTasksAnalyzer(),
            "src/Api/Features/Reference/WarmupService.cs");
    }
}
