using FluentAssertions;
using Xunit;

namespace Meridian.Analyzers.Tests;

public sealed class MER0013RespectBackendLayerBoundariesAnalyzerTests
{
    [Fact]
    public async Task ReportsInfrastructureUsingFromCoreProjectAsync()
    {
        const string source = """
using Meridian.Infrastructure.Database;

namespace Meridian.Core.Services;

public sealed class SampleService
{
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "apps/backend/Meridian.Core/Services/SampleService.cs");

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0013RespectBackendLayerBoundariesAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsEntityFrameworkUsingFromCoreProjectAsync()
    {
        const string source = """
using Microsoft.EntityFrameworkCore;

namespace Meridian.Core.Services;

public sealed class SampleService
{
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "apps/backend/Meridian.Core/Services/SampleService.cs");

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0013RespectBackendLayerBoundariesAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsFullyQualifiedInfrastructureReferenceFromCoreProjectAsync()
    {
        const string source = """
namespace Meridian.Core.Services;

public sealed class SampleService
{
    private Meridian.Infrastructure.Database.MeridianDbContext? _dbContext;
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "apps/backend/Meridian.Core/Services/SampleService.cs");

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0013RespectBackendLayerBoundariesAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportSharedUsingFromCoreProjectAsync()
    {
        const string source = """
using Meridian.Shared.Constants;

namespace Meridian.Core.Services;

public sealed class SampleService
{
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "apps/backend/Meridian.Core/Services/SampleService.cs");

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ReportsApiUsingFromInfrastructureProjectAsync()
    {
        const string source = """
using Meridian.API.Features.Reports.Services;

namespace Meridian.Infrastructure.Services;

public sealed class SampleService
{
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "apps/backend/Meridian.Infrastructure/Services/SampleService.cs");

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0013RespectBackendLayerBoundariesAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsFullyQualifiedApiReferenceFromInfrastructureProjectAsync()
    {
        const string source = """
namespace Meridian.Infrastructure.Services;

public sealed class SampleService
{
    private Meridian.API.Features.Reports.Services.ReportQueueService? _queueService;
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "apps/backend/Meridian.Infrastructure/Services/SampleService.cs");

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0013RespectBackendLayerBoundariesAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsCoreUsingFromSharedProjectAsync()
    {
        const string source = """
using Meridian.Core.Models;

namespace Meridian.Shared.Models;

public sealed class SampleModel
{
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "apps/backend/Meridian.Shared/Models/SampleModel.cs");

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0013RespectBackendLayerBoundariesAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsStandardDatabaseUsingFromAnalyticsProjectAsync()
    {
        const string source = """
using Meridian.Infrastructure.Database;

namespace Meridian.Analytics.Services;

public sealed class AnalyticsService
{
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "apps/backend/Meridian.Analytics/Services/AnalyticsService.cs");

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0013RespectBackendLayerBoundariesAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsMeridianDbContextIdentifierFromAnalyticsProjectAsync()
    {
        const string source = """
namespace Meridian.Analytics.Services;

public sealed class AnalyticsService
{
    private readonly MeridianDbContext _dbContext;
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "apps/backend/Meridian.Analytics/Services/AnalyticsService.cs");

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0013RespectBackendLayerBoundariesAnalyzer.DiagnosticId);
    }

    private static async Task<IReadOnlyCollection<Microsoft.CodeAnalysis.Diagnostic>> GetDiagnosticsAsync(
        string source,
        string path)
    {
        return await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0013RespectBackendLayerBoundariesAnalyzer(),
            path);
    }
}
