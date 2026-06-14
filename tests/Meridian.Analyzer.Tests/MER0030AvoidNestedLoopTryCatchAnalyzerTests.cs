using FluentAssertions;
using Xunit;

namespace Meridian.Analyzer.Tests;

public sealed class MER0030AvoidNestedLoopTryCatchAnalyzerTests
{
    [Fact]
    public async Task ReportsBroadPerIterationTryCatchInsideWhileAndOuterTryAsync()
    {
        const string source = """
using System;
using System.Threading;
using System.Threading.Tasks;

public interface ILogger
{
    void Error(Exception exception, string message);
}

public sealed class Sample
{
    private readonly ILogger _logger = default!;

    public async Task RunAsync(CancellationToken token)
    {
        try
        {
            while (await WaitForNextTickAsync(token))
            {
                try
                {
                    await CollectAsync(token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Collect failed.");
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }

    private static Task<bool> WaitForNextTickAsync(CancellationToken token) => Task.FromResult(!token.IsCancellationRequested);
    private static Task CollectAsync(CancellationToken token) => Task.CompletedTask;
}
""";

        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0030AvoidNestedLoopTryCatchAnalyzer());

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0030AvoidNestedLoopTryCatchAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportBroadTryCatchInsideWhileWithoutOuterTryAsync()
    {
        const string source = """
using System;

public interface ILogger
{
    void Error(Exception exception, string message);
}

public sealed class Sample
{
    private readonly ILogger _logger = default!;

    public void Run(bool shouldContinue)
    {
        while (shouldContinue)
        {
            try
            {
                Execute();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Execute failed.");
            }
        }
    }

    private static void Execute()
    {
    }
}
""";

        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0030AvoidNestedLoopTryCatchAnalyzer());

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportSpecificInnerCatchAsync()
    {
        const string source = """
using System;

public sealed class Sample
{
    public void Run(bool shouldContinue)
    {
        try
        {
            while (shouldContinue)
            {
                try
                {
                    _ = int.Parse("abc");
                }
                catch (FormatException)
                {
                }
            }
        }
        catch (Exception)
        {
        }
    }
}
""";

        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0030AvoidNestedLoopTryCatchAnalyzer());

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportBroadCatchThatReturnsAsync()
    {
        const string source = """
using System;

public sealed class Sample
{
    public int Run(bool shouldContinue)
    {
        try
        {
            while (shouldContinue)
            {
                try
                {
                    return int.Parse("abc");
                }
                catch (Exception)
                {
                    return 0;
                }
            }

            return 1;
        }
        catch (Exception)
        {
            return -1;
        }
    }
}
""";

        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0030AvoidNestedLoopTryCatchAnalyzer());

        diagnostics.Should().BeEmpty();
    }
}
