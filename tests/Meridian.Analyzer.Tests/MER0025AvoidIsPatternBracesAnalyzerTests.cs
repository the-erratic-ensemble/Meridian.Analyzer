using FluentAssertions;
using Xunit;

namespace Meridian.Analyzer.Tests;

public sealed class MER0025AvoidIsPatternBracesAnalyzerTests
{
    [Fact]
    public async Task ReportsDiagnostic_ForNullableExtractionPatternAsync()
    {
        const string source = """
public sealed class DensityService
{
    public decimal? Calculate(decimal? population)
    {
        if (population is { } populationValue)
        {
            return populationValue;
        }

        return null;
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0025AvoidIsPatternBracesAnalyzer.DiagnosticId);
        diagnostics[0].GetMessage().Should().Contain("{ } populationValue");
    }

    [Fact]
    public async Task ReportsDiagnostic_ForNegatedEmptyPatternAsync()
    {
        const string source = """
public sealed class DensityService
{
    public bool IsMissing(decimal? population)
    {
        return population is not { };
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0025AvoidIsPatternBracesAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReport_ForNegatedCollectionPatternWithMembersAsync()
    {
        const string source = """
using System.Collections.Generic;

public sealed class DomainService
{
    public bool HasMissingCodes(IReadOnlyCollection<string>? codes)
    {
        return codes is not { Count: > 0 };
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ReportsDiagnostic_ForNestedEmptyPropertyPatternAsync()
    {
        const string source = """
public sealed class Envelope
{
    public Payload? Payload { get; set; }
}

public sealed class Payload
{
    public string? Value { get; set; }
}

public sealed class SnapshotService
{
    public Payload? Resolve(Envelope envelope)
    {
        if (envelope is { Payload: { } payload })
        {
            return payload;
        }

        return null;
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0025AvoidIsPatternBracesAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReport_ForNestedPropertyPatternWithMembersAsync()
    {
        const string source = """
using System.Collections.Generic;

public sealed class Wrapper
{
    public Payload? Value { get; set; }
}

public sealed class Payload
{
    public IReadOnlyList<string>? Data { get; set; }
}

public sealed class SnapshotService
{
    public bool HasData(Wrapper wrapper)
    {
        return wrapper is { Value.Data: { Count: > 0 } };
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ReportsDiagnostic_ForTupleBracePatternAsync()
    {
        const string source = """
public sealed class CentroidService
{
    public (double Latitude, double Longitude)? Resolve((double Latitude, double Longitude)? localAuthorityCentroid)
    {
        if (localAuthorityCentroid is ({ }, { }))
        {
            return localAuthorityCentroid;
        }

        return null;
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0025AvoidIsPatternBracesAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReport_ForNonBracePatternsAsync()
    {
        const string source = """
public sealed class DensityService
{
    public bool IsMissing(decimal? population)
    {
        return population is null;
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReport_ForDomainPropertyShapePatternAsync()
    {
        const string source = """
public sealed class Feature
{
    public bool IsTrial { get; init; }
    public System.DateTime? TrialEndsAt { get; init; }
}

public sealed class FeatureService
{
    public bool IsActiveTrial(Feature feature)
    {
        return feature is { IsTrial: true, TrialEndsAt: not null };
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReport_InTestPathAsync()
    {
        const string source = """
public sealed class DensityTests
{
    public bool HasValue(decimal? population)
    {
        return population is { } populationValue && populationValue > 0;
    }
}
""";

        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0025AvoidIsPatternBracesAnalyzer(),
            "apps/backend/tests/Meridian.API.Tests/Services/DensityTests.cs");

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReport_InAnalyzerPackagePathAsync()
    {
        const string source = """
public sealed class PatternWalker
{
    public bool HasValue(decimal? population)
    {
        return population is { } populationValue && populationValue > 0;
    }
}
""";

        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0025AvoidIsPatternBracesAnalyzer(),
            "apps/backend/Meridian.Analyzer/PatternWalker.cs");

        diagnostics.Should().BeEmpty();
    }

    private static async Task<IReadOnlyList<Microsoft.CodeAnalysis.Diagnostic>> GetDiagnosticsAsync(string source)
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0025AvoidIsPatternBracesAnalyzer(),
            "apps/backend/Meridian.API/Features/Reference/DensityService.cs");

        return diagnostics;
    }
}
