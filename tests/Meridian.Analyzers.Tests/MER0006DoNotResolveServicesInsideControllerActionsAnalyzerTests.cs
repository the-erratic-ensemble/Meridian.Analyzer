using FluentAssertions;
using Xunit;

namespace Meridian.Analyzers.Tests;

public sealed class MER0006DoNotResolveServicesInsideControllerActionsAnalyzerTests
{
    [Fact]
    public async Task ReportsRequestServicesResolutionInsideControllerActionAsync()
    {
        const string source = """
public sealed class ReportStatusController : BaseApiController
{
    [HttpGet]
    public object GetStatus()
    {
        var service = HttpContext.RequestServices.GetRequiredService<IReportService>();
        return service;
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0006DoNotResolveServicesInsideControllerActionsAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsServiceProviderResolutionInsideControllerActionAsync()
    {
        const string source = """
public sealed class ReportStatusController : BaseApiController
{
    private readonly IServiceProvider _serviceProvider;

    public ReportStatusController(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    [HttpGet]
    public object GetStatus()
    {
        return _serviceProvider.GetService<IReportService>()!;
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0006DoNotResolveServicesInsideControllerActionsAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsActionScopedServiceProviderEvenWhenParameterNameIsGenericAsync()
    {
        const string source = """
using System;

public static class ServiceProviderServiceExtensions
{
    public static T GetRequiredService<T>(this IServiceProvider provider) => default!;
}

public sealed class ReportStatusController : BaseApiController
{
    [HttpGet]
    public object GetStatus(IServiceProvider provider)
    {
        return provider.GetRequiredService<IReportService>()!;
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0006DoNotResolveServicesInsideControllerActionsAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportNonServiceProviderTargetWithServiceProviderNameAsync()
    {
        const string source = """
public sealed class ReportStatusController : BaseApiController
{
    [HttpGet]
    public object GetStatus(string serviceProvider)
    {
        return serviceProvider.GetRequiredService<IReportService>()!;
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportRequestServicesResolutionOutsideControllerActionAsync()
    {
        const string source = """
public sealed class ReportControllerCoordinator
{
    public object Build(HttpContext httpContext)
    {
        return httpContext.RequestServices.GetRequiredService<IReportService>();
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportFromServicesActionParameterAsync()
    {
        const string source = """
public sealed class ReportStatusController : BaseApiController
{
    [HttpGet]
    public object GetStatus([FromServices] IReportService reportService)
    {
        return reportService;
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    private static async Task<IReadOnlyCollection<Microsoft.CodeAnalysis.Diagnostic>> GetDiagnosticsAsync(string source)
    {
        return await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0006DoNotResolveServicesInsideControllerActionsAnalyzer());
    }
}
