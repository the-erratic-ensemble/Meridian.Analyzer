using System.Collections.Immutable;
using Meridian.Analyzer;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Meridian.Analyzer.Tests;

public sealed class MER0001DoNotUseConditionalExpressionsInInitializersAnalyzerTests
{
    [Fact]
    public async Task ReportsConditionalExpressionInAnonymousObjectMemberWhenBranchBuildsPayloadAsync()
    {
        const string source = """
using System;

public static class Sample
{
    public static object Build(bool includeTenant, string slug)
    {
        return new
        {
            tenant = includeTenant
                ? new
                {
                    slug
                }
                : null
        };
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0001DoNotUseConditionalExpressionsInInitializersAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsConditionalExpressionInObjectInitializerAssignmentWhenBranchBuildsPayloadAsync()
    {
        const string source = """
using System;

public sealed class TenantPayload
{
    public string? Slug { get; set; }
}

public sealed class SamplePayload
{
    public TenantPayload? Tenant { get; set; }
}

public static class Sample
{
    public static SamplePayload Build(bool includeTenant, string slug)
    {
        return new SamplePayload
        {
            Tenant = includeTenant
                ? new TenantPayload
                {
                    Slug = slug
                }
                : null
        };
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0001DoNotUseConditionalExpressionsInInitializersAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportConditionalExpressionNestedInsideInvocationAsync()
    {
        const string source = """
using System;

public sealed class SamplePayload
{
    public string? Tenant { get; set; }
}

public static class Sample
{
    private static string? Normalize(string? value) => value;

    public static SamplePayload Build(bool includeTenant, string slug)
    {
        return new SamplePayload
        {
            Tenant = Normalize(includeTenant ? slug : null)
        };
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportStagedLocalUsedInInitializerAsync()
    {
        const string source = """
using System;

public static class Sample
{
    public static object Build(bool includeTenant, string slug)
    {
        var tenant = includeTenant ? slug : null;

        return new
        {
            tenant
        };
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ReportsConditionNullOrAnonymousObjectShapeAsync()
    {
        const string source = """
using System;

public static class Sample
{
    public static object Build(bool includeTenant, string slug)
    {
        return new
        {
            tenant = includeTenant
                ? null
                : new
                {
                    slug
                }
        };
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0001DoNotUseConditionalExpressionsInInitializersAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportScalarConditionalExpressionInAnonymousObjectMemberAsync()
    {
        const string source = """
using System;

public static class Sample
{
    public static object Build(bool includeTenant, string slug)
    {
        return new
        {
            tenant = includeTenant ? slug : null
        };
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportScalarConditionalExpressionInObjectInitializerAssignmentAsync()
    {
        const string source = """
using System;

public sealed class SamplePayload
{
    public string? OverrideId { get; set; }
}

public static class Sample
{
    public static SamplePayload Build(bool isActive, string? overrideId)
    {
        return new SamplePayload
        {
            OverrideId = isActive ? overrideId : null
        };
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportConditionalExpressionInDictionaryElementInitializerAsync()
    {
        const string source = """
using System.Collections.Generic;

public static class Sample
{
    public static Dictionary<string, string?> Build(bool includeTenant, string slug)
    {
        return new Dictionary<string, string?>
        {
            ["tenant"] = includeTenant ? slug : null
        };
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportConditionalExpressionInGeneratedCodeAsync()
    {
        const string source = """
// <auto-generated/>
using System;

public static class Sample
{
    public static object Build(bool includeTenant, string slug)
    {
        return new
        {
            tenant = includeTenant ? slug : null
        };
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, path: "Sample.g.cs");

        diagnostics.Should().BeEmpty();
    }

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source, string path = "Test0.cs")
    {
        return await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0001DoNotUseConditionalExpressionsInInitializersAnalyzer(),
            path);
    }
}
