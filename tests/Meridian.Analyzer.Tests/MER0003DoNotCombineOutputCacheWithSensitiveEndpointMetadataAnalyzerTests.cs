using FluentAssertions;
using Xunit;

namespace Meridian.Analyzer.Tests;

public sealed class MER0003DoNotCombineOutputCacheWithSensitiveEndpointMetadataAnalyzerTests
{
    [Fact]
    public async Task ReportsOutputCacheOnActionWithFeatureConsumptionAsync()
    {
        const string source = """
public sealed class SearchController : ApiControllerBase
{
    [HttpGet]
    [OutputCache(Duration = 120)]
    [ConsumeFeature("search")]
    public object Search() => new();
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0003DoNotCombineOutputCacheWithSensitiveEndpointMetadataAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsOutputCacheInheritedFromClassWhenActionHasTenantMetadataAsync()
    {
        const string source = """
[OutputCache(Duration = 120)]
public sealed class ReportsController : ApiControllerBase
{
    [HttpGet]
    [TenantScoped]
    public object GetReport() => new();
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0003DoNotCombineOutputCacheWithSensitiveEndpointMetadataAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsClassOutputCacheOnlyOnceWhenMultipleActionsHaveSensitiveMetadataAsync()
    {
        const string source = """
[OutputCache(Duration = 120)]
public sealed class ReportsController : ApiControllerBase
{
    [HttpGet]
    [TenantScoped]
    public object GetReport() => new();

    [HttpGet]
    [RequirePlan("Pro")]
    public object GetHistory() => new();
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0003DoNotCombineOutputCacheWithSensitiveEndpointMetadataAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsClassOutputCacheWithClassTenantMetadataOnlyOnceAsync()
    {
        const string source = """
[OutputCache(Duration = 120)]
[TenantScoped]
public sealed class ReportsController : ApiControllerBase
{
    [HttpGet]
    public object GetReport() => new();
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0003DoNotCombineOutputCacheWithSensitiveEndpointMetadataAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportOutputCacheWithoutSensitiveMetadataAsync()
    {
        const string source = """
public sealed class GreenspaceController : ApiControllerBase
{
    [HttpGet]
    [OutputCache(Duration = 120)]
    public object Search() => new();
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ReportsOutputCacheWithExplicitAuthorizationPolicyAsync()
    {
        const string source = """
public sealed class AdminController : ApiControllerBase
{
    [HttpGet]
    [OutputCache(Duration = 120)]
    [Authorize(Policy = AuthorizationPolicies.PlatformAdminOnly)]
    public object Get() => new();
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0003DoNotCombineOutputCacheWithSensitiveEndpointMetadataAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportOutputCacheWithAuthenticationOnlyMetadataAsync()
    {
        const string source = """
public sealed class ReferenceController : ApiControllerBase
{
    [HttpGet]
    [OutputCache(Duration = 120)]
    [Authorize]
    public object Get() => new();
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    private static async Task<IReadOnlyCollection<Microsoft.CodeAnalysis.Diagnostic>> GetDiagnosticsAsync(string source)
    {
        return await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0003DoNotCombineOutputCacheWithSensitiveEndpointMetadataAnalyzer());
    }
}
