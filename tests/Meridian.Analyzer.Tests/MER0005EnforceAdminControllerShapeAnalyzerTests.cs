using FluentAssertions;
using Xunit;

namespace Meridian.Analyzer.Tests;

public sealed class MER0005EnforceAdminControllerShapeAnalyzerTests
{
    [Fact]
    public async Task ReportsAdminRouteWithoutAdminControllerShapeAsync()
    {
        const string source = """
[Route("api/admin/users")]
public sealed class UsersController : ControllerBase
{
    public void Configure()
    {
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "src/Api/Features/Users/Controllers/UsersController.cs");

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0005EnforceAdminControllerShapeAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportAdminControllerWithAdminShapeAsync()
    {
        const string source = """
[Route("api/admin/users")]
public sealed class AdminUsersController : AdminControllerBase
{
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "src/Api/Features/Admin/Controllers/AdminUsersController.cs");

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportAdminControllerPartialWithoutRepeatedShapeAsync()
    {
        const string rootSource = """
[Route("api/admin/platform")]
public sealed partial class AdminPlatformController : AdminControllerBase
{
}
""";
        const string partialSource = """
public sealed partial class AdminPlatformController
{
    public void Export()
    {
    }
}
""";

        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            new[]
            {
                (Source: rootSource, Path: "src/Api/Features/Admin/Controllers/AdminPlatformController.cs"),
                (Source: partialSource, Path: "src/Api/Features/Admin/Controllers/AdminPlatformController.Exports.cs")
            },
            new MER0005EnforceAdminControllerShapeAnalyzer());

        diagnostics.Should().BeEmpty();
    }

    private static async Task<IReadOnlyCollection<Microsoft.CodeAnalysis.Diagnostic>> GetDiagnosticsAsync(
        string source,
        string path)
    {
        return await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0005EnforceAdminControllerShapeAnalyzer(),
            path);
    }
}
