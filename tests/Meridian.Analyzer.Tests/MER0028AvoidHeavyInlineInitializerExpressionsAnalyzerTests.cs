using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Meridian.Analyzer.Tests;

public sealed class MER0028AvoidHeavyInlineInitializerExpressionsAnalyzerTests
{
    [Fact]
    public async Task ReportsDiagnostic_ForHeavyAnonymousObjectMemberExpressionAsync()
    {
        const string source = """
using System;

public sealed class StopRow
{
    public string? TransportModeName { get; set; }
    public string? StopTypeName { get; set; }
    public string? CommonName { get; set; }
}

public static class Sample
{
    public static object Build(StopRow row)
    {
        return new
        {
            RailPriority =
                row.TransportModeName == "rail"
                || row.TransportModeName == "train"
                    ? 1
                    : row.CommonName == "rail"
                        || row.CommonName == "train"
                            ? 2
                            : row.CommonName == "station"
                                && row.TransportModeName != "bus"
                                && row.StopTypeName != "bus"
                                && row.TransportModeName != "tram"
                                && row.StopTypeName != "tram"
                                    ? 3
                                    : 99
        };
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0028AvoidHeavyInlineInitializerExpressionsAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsDiagnostic_ForHeavyObjectInitializerAssignmentAsync()
    {
        const string source = """
public sealed class Payload
{
    public int Priority { get; set; }
}

public static class Sample
{
    public static Payload Build(bool a, bool b, bool c, bool d, bool e, bool f, bool g)
    {
        return new Payload
        {
            Priority =
                a
                    ? 0
                    : b
                        ? 1
                        : c
                            ? 2
                            : d && e && f && g && !a && !b
                                ? 3
                                : 99
        };
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0028AvoidHeavyInlineInitializerExpressionsAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReport_ForShortInitializerMemberExpressionAsync()
    {
        const string source = """
public sealed class Payload
{
    public int Priority { get; set; }
}

public static class Sample
{
    public static Payload Build(bool isRailCode, bool hasRailHint)
    {
        return new Payload
        {
            Priority = isRailCode ? 0 : hasRailHint ? 1 : 99
        };
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReport_ForStagedInitializerMemberValueAsync()
    {
        const string source = """
public sealed class Payload
{
    public int Priority { get; set; }
}

public static class Sample
{
    public static Payload Build(bool a, bool b, bool c, bool d, bool e, bool f)
    {
        var priority =
            a || b
                ? 0
                : c || d
                    ? 1
                    : e && f
                        ? 2
                        : 99;

        return new Payload
        {
            Priority = priority
        };
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReport_ForNestedObjectConstructionWithMostlyMappedMembersAsync()
    {
        const string source = """
using System;

public sealed class OuterPayload
{
    public InnerPayload? Quota { get; set; }
}

public sealed class InnerPayload
{
    public string? FeatureKey { get; set; }
    public int? Limit { get; set; }
    public int? Remaining { get; set; }
    public bool IsDegraded { get; set; }
    public string? LimitSource { get; set; }
}

public sealed class ReadinessState
{
    public int? Remaining { get; set; }
    public bool BalanceIsDegraded { get; set; }
    public string SnapshotStatus { get; set; } = "";
    public string LimitSource { get; set; } = "";
}

public static class Sample
{
    public static OuterPayload Build(ReadinessState readinessState)
    {
        return new OuterPayload
        {
            Quota = new InnerPayload
            {
                FeatureKey = "quota",
                Limit = 10,
                Remaining = readinessState.Remaining.HasValue ? Math.Max(readinessState.Remaining.Value, 0) : null,
                IsDegraded = readinessState.BalanceIsDegraded
                    || !string.Equals(readinessState.SnapshotStatus, "available", StringComparison.Ordinal)
                    || !string.Equals(readinessState.LimitSource, "live_snapshot", StringComparison.Ordinal),
                LimitSource = readinessState.LimitSource
            }
        };
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ReportsDiagnostic_ForHeavyInlinePipelineInInitializerMemberAsync()
    {
        const string source = """
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class ScopePayload
{
    public string[] AudienceProfiles { get; set; } = Array.Empty<string>();
}

public sealed class Profile
{
    public string Key { get; set; } = "";
    public bool IsAnonymous { get; set; }
    public IEnumerable<string> VisibleDomains { get; set; } = Array.Empty<string>();
    public IEnumerable<string> EnabledFeatureKeys { get; set; } = Array.Empty<string>();
}

public static class Sample
{
    public static ScopePayload Build(IEnumerable<Profile> profiles)
    {
        return new ScopePayload
        {
            AudienceProfiles = profiles
                .Select(profile => new
                {
                    profile.Key,
                    profile.IsAnonymous,
                    VisibleDomains = profile.VisibleDomains
                        .Select(domain => domain.ToLowerInvariant())
                        .OrderBy(domain => domain, StringComparer.Ordinal)
                        .ToArray(),
                    EnabledFeatureKeys = profile.EnabledFeatureKeys
                        .OrderBy(featureKey => featureKey, StringComparer.Ordinal)
                        .ToArray()
                })
                .OrderBy(profile => profile.Key, StringComparer.Ordinal)
                .Select(profile => profile.Key)
                .ToArray()
        };
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0028AvoidHeavyInlineInitializerExpressionsAnalyzer.DiagnosticId);
    }

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source, string path = "src/Infrastructure/Repositories/Repo.cs")
    {
        return await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0028AvoidHeavyInlineInitializerExpressionsAnalyzer(),
            path);
    }
}
