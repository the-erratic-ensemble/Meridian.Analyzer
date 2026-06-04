using FluentAssertions;
using Xunit;

namespace Meridian.Analyzers.Tests;

public sealed class MER0002DoNotNestTryCatchInTryBlockAnalyzerTests
{
    [Fact]
    public async Task ReportsNestedTryCatchInsideOuterTryBlockAsync()
    {
        const string source = """
using System;

public static class Sample
{
    public static void Run(bool shouldParse, string value)
    {
        try
        {
            if (shouldParse)
            {
                try
                {
                    _ = int.Parse(value);
                }
                catch
                {
                    _ = 0;
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
            new MER0002DoNotNestTryCatchInTryBlockAnalyzer());

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0002DoNotNestTryCatchInTryBlockAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportNestedTryCatchWithSpecificExceptionTypeAsync()
    {
        const string source = """
using System;

public static class Sample
{
    public static void Run()
    {
        try
        {
            try
            {
                _ = int.Parse("abc");
            }
            catch (FormatException)
            {
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
            new MER0002DoNotNestTryCatchInTryBlockAnalyzer());

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportNestedTryCatchThatReturnsFromCatchAsync()
    {
        const string source = """
using System;

public static class Sample
{
    public static int Run(string value)
    {
        try
        {
            try
            {
                return int.Parse(value);
            }
            catch
            {
                return 0;
            }
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
            new MER0002DoNotNestTryCatchInTryBlockAnalyzer());

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportLogOnlyBroadCatchInsideOuterTryBlockAsync()
    {
        const string source = """
using System;

public interface ILogger
{
    void Warning(Exception exception, string message);
}

public static class Sample
{
    private static ILogger _logger = default!;

    public static void Run(string value)
    {
        try
        {
            try
            {
                _ = int.Parse(value);
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to parse value");
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
            new MER0002DoNotNestTryCatchInTryBlockAnalyzer());

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ReportsBroadCatchThatLogsAndMutatesFallbackStateAsync()
    {
        const string source = """
using System;
using System.Collections.Generic;

public interface ILogger
{
    void Warning(Exception exception, string message);
}

public static class Sample
{
    private static ILogger _logger = default!;

    public static void Run(string value)
    {
        try
        {
            var failures = new List<string>();

            try
            {
                _ = int.Parse(value);
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to parse value");
                failures.Add(value);
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
            new MER0002DoNotNestTryCatchInTryBlockAnalyzer());

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0002DoNotNestTryCatchInTryBlockAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportNestedTryFinallyInsideOuterTryBlockAsync()
    {
        const string source = """
using System;

public static class Sample
{
    public static void Run()
    {
        try
        {
            try
            {
            }
            finally
            {
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
            new MER0002DoNotNestTryCatchInTryBlockAnalyzer());

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportNestedTryCatchInsideFinallyAsync()
    {
        const string source = """
using System;

public static class Sample
{
    public static void Run()
    {
        try
        {
        }
        finally
        {
            try
            {
                _ = 1;
            }
            catch (Exception)
            {
            }
        }
    }
}
""";

        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0002DoNotNestTryCatchInTryBlockAnalyzer());

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportTryCatchInsideLocalFunctionDeclaredInTryBlockAsync()
    {
        const string source = """
using System;

public static class Sample
{
    public static void Run(string value)
    {
        try
        {
            void ParseValue()
            {
                try
                {
                    _ = int.Parse(value);
                }
                catch
                {
                    _ = 0;
                }
            }

            ParseValue();
        }
        catch (Exception)
        {
        }
    }
}
""";

        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0002DoNotNestTryCatchInTryBlockAnalyzer());

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportTryCatchInsideLambdaDeclaredInTryBlockAsync()
    {
        const string source = """
using System;

public static class Sample
{
    public static void Run(string value)
    {
        try
        {
            Action parseValue = () =>
            {
                try
                {
                    _ = int.Parse(value);
                }
                catch
                {
                    _ = 0;
                }
            };

            parseValue();
        }
        catch (Exception)
        {
        }
    }
}
""";

        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0002DoNotNestTryCatchInTryBlockAnalyzer());

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportNestedTryCatchInGeneratedCodeAsync()
    {
        const string source = """
// <auto-generated/>
using System;

public static class Sample
{
    public static void Run(string value)
    {
        try
        {
            try
            {
                _ = int.Parse(value);
            }
            catch
            {
                _ = 0;
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
            new MER0002DoNotNestTryCatchInTryBlockAnalyzer(),
            path: "Sample.g.cs");

        diagnostics.Should().BeEmpty();
    }
}
