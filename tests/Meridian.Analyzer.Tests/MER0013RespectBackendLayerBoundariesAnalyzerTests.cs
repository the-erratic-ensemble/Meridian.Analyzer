using FluentAssertions;
using Xunit;

namespace Meridian.Analyzer.Tests;

public sealed class MER0013RespectBackendLayerBoundariesAnalyzerTests
{
    [Fact]
    public async Task ReportsInfrastructureUsingFromCoreProjectAsync()
    {
        const string source = """
using Infrastructure.Database;

namespace Core.Services;

public sealed class SampleService
{
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "src/Core/Services/SampleService.cs");

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0013RespectBackendLayerBoundariesAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsEntityFrameworkUsingFromCoreProjectAsync()
    {
        const string source = """
using Microsoft.EntityFrameworkCore;

namespace Core.Services;

public sealed class SampleService
{
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "src/Core/Services/SampleService.cs");

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0013RespectBackendLayerBoundariesAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsFullyQualifiedInfrastructureReferenceFromCoreProjectAsync()
    {
        const string source = """
namespace Core.Services;

public sealed class SampleService
{
    private Infrastructure.Database.AppDbContext? _dbContext;
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "src/Core/Services/SampleService.cs");

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0013RespectBackendLayerBoundariesAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportSharedUsingFromCoreProjectAsync()
    {
        const string source = """
using Shared.Constants;

namespace Core.Services;

public sealed class SampleService
{
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "src/Core/Services/SampleService.cs");

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ReportsApiUsingFromInfrastructureProjectAsync()
    {
        const string source = """
using Api.Features.Reports.Services;

namespace Infrastructure.Services;

public sealed class SampleService
{
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "src/Infrastructure/Services/SampleService.cs");

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0013RespectBackendLayerBoundariesAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsFullyQualifiedApiReferenceFromInfrastructureProjectAsync()
    {
        const string source = """
namespace Infrastructure.Services;

public sealed class SampleService
{
    private Api.Features.Reports.Services.ReportQueueService? _queueService;
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "src/Infrastructure/Services/SampleService.cs");

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0013RespectBackendLayerBoundariesAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsCoreUsingFromSharedProjectAsync()
    {
        const string source = """
using Core.Models;

namespace Shared.Models;

public sealed class SampleModel
{
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "src/Shared/Models/SampleModel.cs");

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0013RespectBackendLayerBoundariesAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsStandardDatabaseUsingFromAnalyticsProjectAsync()
    {
        const string source = """
using Infrastructure.Database;

namespace Analytics.Services;

public sealed class AnalyticsService
{
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "src/Analytics/Services/AnalyticsService.cs");

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0013RespectBackendLayerBoundariesAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsAppDbContextIdentifierFromAnalyticsProjectAsync()
    {
        const string source = """
namespace Analytics.Services;

public sealed class AnalyticsService
{
    private readonly AppDbContext _dbContext;
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "src/Analytics/Services/AnalyticsService.cs");

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
