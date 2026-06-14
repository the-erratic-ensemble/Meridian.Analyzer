using FluentAssertions;
using Xunit;

namespace Meridian.Analyzer.Tests;

public sealed class MER0031AvoidNestedCollectionTrimWhileLoopsAnalyzerTests
{
    [Fact]
    public async Task ReportsNestedQueueTrimWhileLoopInsideOuterWhileAsync()
    {
        const string source = """
using System.Collections.Concurrent;

public sealed class Sample
{
    private readonly ConcurrentQueue<int> _snapshots = new();

    public void Run(bool shouldContinue)
    {
        while (shouldContinue)
        {
            _snapshots.Enqueue(1);

            while (_snapshots.Count > 100)
            {
                _snapshots.TryDequeue(out _);
            }
        }
    }
}
""";

        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0031AvoidNestedCollectionTrimWhileLoopsAnalyzer());

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0031AvoidNestedCollectionTrimWhileLoopsAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsNestedListTrimWhenCountComparisonIsReversedAsync()
    {
        const string source = """
using System.Collections.Generic;

public sealed class Sample
{
    private readonly List<int> _history = new();

    public void Run(bool shouldContinue)
    {
        while (shouldContinue)
        {
            _history.Add(1);

            while (10 < _history.Count)
            {
                _history.RemoveAt(0);
            }
        }
    }
}
""";

        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0031AvoidNestedCollectionTrimWhileLoopsAnalyzer());

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0031AvoidNestedCollectionTrimWhileLoopsAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportTopLevelTrimWhileLoopAsync()
    {
        const string source = """
using System.Collections.Generic;

public sealed class Sample
{
    private readonly List<int> _history = new();

    public void Run()
    {
        while (_history.Count > 10)
        {
            _history.RemoveAt(0);
        }
    }
}
""";

        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0031AvoidNestedCollectionTrimWhileLoopsAnalyzer());

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportNestedWhileLoopThatDoesMoreThanTrimTrackedCollectionAsync()
    {
        const string source = """
using System.Collections.Generic;

public sealed class Sample
{
    private readonly List<int> _history = new();

    public void Run(bool shouldContinue)
    {
        var cursor = 0;

        while (shouldContinue)
        {
            while (_history.Count > 10)
            {
                cursor++;
            }
        }
    }
}
""";

        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0031AvoidNestedCollectionTrimWhileLoopsAnalyzer());

        diagnostics.Should().BeEmpty();
    }
}
