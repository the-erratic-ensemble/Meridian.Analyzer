using FluentAssertions;
using Xunit;

namespace Meridian.Analyzers.Tests;

public sealed class MER0004RequireExplicitControllerAuthorizationPolicyAnalyzerTests
{
    [Fact]
    public async Task ReportsAdminControllerWithoutExplicitPolicyAsync()
    {
        const string source = """
[Route("api/admin/users")]
public sealed class AdminUsersController : BaseAdminController
{
    [HttpGet]
    public object ListUsers() => new();
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "apps/backend/Meridian.API/Features/Admin/Controllers/AdminUsersController.cs");

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0004RequireExplicitControllerAuthorizationPolicyAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportAdminControllerWithClassPolicyAsync()
    {
        const string source = """
[Route("api/admin/users")]
[Authorize(Policy = AuthorizationPolicies.PlatformAdminOnly)]
public sealed class AdminUsersController : BaseAdminController
{
    [HttpGet]
    public object ListUsers() => new();
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "apps/backend/Meridian.API/Features/Admin/Controllers/AdminUsersController.cs");

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportPartialAdminControllerWhenClassPolicyIsDeclaredOnAnotherPartialAsync()
    {
        const string metadataSource = """
[Route("api/admin/reports")]
[Authorize(Policy = AuthorizationPolicies.PlatformAdminOnly)]
public sealed partial class AdminReportsController : BaseAdminController
{
}
""";
        const string actionsSource = """
public sealed partial class AdminReportsController
{
    [HttpGet]
    public object ListReports() => new();
}
""";

        var diagnostics = await GetDiagnosticsAsync(
            new[] {
                (Source: metadataSource, Path: "apps/backend/Meridian.API/Features/Admin/Controllers/AdminReportsController.cs"),
                (Source: actionsSource, Path: "apps/backend/Meridian.API/Features/Admin/Controllers/AdminReportsController.Actions.cs")
            });

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ReportsPartialAdminControllerWhenActionsOnAnotherPartialHaveNoExplicitPolicyAsync()
    {
        const string metadataSource = """
[Route("api/admin/reports")]
public sealed partial class AdminReportsController : BaseAdminController
{
}
""";
        const string actionsSource = """
public sealed partial class AdminReportsController
{
    [HttpGet]
    public object ListReports() => new();
}
""";

        var diagnostics = await GetDiagnosticsAsync(
            new[] {
                (Source: metadataSource, Path: "apps/backend/Meridian.API/Features/Admin/Controllers/AdminReportsController.cs"),
                (Source: actionsSource, Path: "apps/backend/Meridian.API/Features/Admin/Controllers/AdminReportsController.Actions.cs")
            });

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0004RequireExplicitControllerAuthorizationPolicyAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportAdminControllerWhenEveryActionHasPolicyAsync()
    {
        const string source = """
[Route("api/admin/platform")]
public sealed class AdminPlatformController : BaseAdminController
{
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.PlatformAdminOnly)]
    public object Health() => new();
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "apps/backend/Meridian.API/Features/Admin/Controllers/AdminPlatformController.cs");

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ReportsHighRiskControllerWithoutExplicitPolicyAsync()
    {
        const string source = """
[Route("api/reports")]
public sealed class ReportStatusController : BaseApiController
{
    [HttpGet]
    public object GetStatus() => new();
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "apps/backend/Meridian.API/Features/Reports/Controllers/ReportStatusController.cs");

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0004RequireExplicitControllerAuthorizationPolicyAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsUnexpectedAllowAnonymousOutsideApprovedSurfacesAsync()
    {
        const string source = """
[Route("api/reports")]
public sealed class ReportStatusController : BaseApiController
{
    [HttpGet]
    [AllowAnonymous]
    public object GetStatus() => new();
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "apps/backend/Meridian.API/Features/Reports/Controllers/ReportStatusController.cs");

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0004RequireExplicitControllerAuthorizationPolicyAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportAllowAnonymousInApprovedDevSurfaceAsync()
    {
        const string source = """
[Route("api/dev")]
public sealed class DevController : BaseApiController
{
    [HttpGet]
    [AllowAnonymous]
    public object Token() => new();
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "apps/backend/Meridian.API/Features/Dev/Controllers/DevController.cs");

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportApprovedAnonymousAnalyticsEventsActionsAsync()
    {
        const string source = """
namespace Meridian.Analytics.Controllers;

[Route("api/[controller]")]
public sealed class EventsController : ControllerBase
{
    [HttpPost("single")]
    [AllowAnonymous]
    public object SubmitEvent() => new();

    [HttpPost("batch")]
    [AllowAnonymous]
    public object SubmitEvents() => new();

    [HttpPost]
    [AllowAnonymous]
    public object SubmitEventsDefault() => new();

    [HttpGet("health")]
    [AllowAnonymous]
    public object Health() => new();
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "apps/backend/Meridian.Analytics/Controllers/EventsController.cs");

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ReportsClassLevelAllowAnonymousOnAnalyticsEventsControllerAsync()
    {
        const string source = """
namespace Meridian.Analytics.Controllers;

[Route("api/[controller]")]
[AllowAnonymous]
public sealed class EventsController : ControllerBase
{
    [HttpPost("single")]
    public object SubmitEvent() => new();
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "apps/backend/Meridian.Analytics/Controllers/EventsController.cs");

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0004RequireExplicitControllerAuthorizationPolicyAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsUnexpectedAnonymousAnalyticsEventsActionAsync()
    {
        const string source = """
namespace Meridian.Analytics.Controllers;

[Route("api/[controller]")]
public sealed class EventsController : ControllerBase
{
    [HttpDelete]
    [AllowAnonymous]
    public object DeleteEvents() => new();
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "apps/backend/Meridian.Analytics/Controllers/EventsController.cs");

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0004RequireExplicitControllerAuthorizationPolicyAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportApprovedAnonymousAnalyticsEventsActionsAfterFileMoveAsync()
    {
        const string source = """
namespace Meridian.Analytics.Controllers;

[Route("api/[controller]")]
public sealed class EventsController : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    public object SubmitEventsDefault() => new();
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "apps/backend/Meridian.Analytics/Features/EventIngestion/EventsController.cs");

        diagnostics.Should().BeEmpty();
    }

    private static async Task<IReadOnlyCollection<Microsoft.CodeAnalysis.Diagnostic>> GetDiagnosticsAsync(
        string source,
        string path)
    {
        return await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0004RequireExplicitControllerAuthorizationPolicyAnalyzer(),
            path);
    }

    private static async Task<IReadOnlyCollection<Microsoft.CodeAnalysis.Diagnostic>> GetDiagnosticsAsync(
        IReadOnlyCollection<(string Source, string Path)> sources)
    {
        return await AnalyzerTestHost.GetDiagnosticsAsync(
            sources,
            new MER0004RequireExplicitControllerAuthorizationPolicyAnalyzer());
    }
}
