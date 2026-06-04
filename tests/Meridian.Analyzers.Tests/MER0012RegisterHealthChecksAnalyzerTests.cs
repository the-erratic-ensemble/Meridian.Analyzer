using FluentAssertions;
using Xunit;

namespace Meridian.Analyzers.Tests;

public sealed class MER0012RegisterHealthChecksAnalyzerTests
{
    [Fact]
    public async Task ReportsUnregisteredHealthCheckAsync()
    {
        const string source = """
namespace Microsoft.Extensions.Diagnostics.HealthChecks;

public interface IHealthCheck
{
}

public sealed class ReadyHealthCheck : IHealthCheck
{
}
""";

        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0012RegisterHealthChecksAnalyzer(),
            "apps/backend/Meridian.API/Health/ReadyHealthCheck.cs",
            assemblyName: "Meridian.API");

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0012RegisterHealthChecksAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportRegisteredHealthCheckAsync()
    {
        const string healthCheckSource = """
namespace Microsoft.Extensions.Diagnostics.HealthChecks;

public interface IHealthCheck
{
}

public sealed class ReadyHealthCheck : IHealthCheck
{
}
""";
        const string registrationSource = """
using Microsoft.Extensions.Diagnostics.HealthChecks;

public sealed class HealthRegistration
{
    public void Add(dynamic builder)
    {
        builder.AddCheck<ReadyHealthCheck>("ready");
    }
}
""";

        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            new[]
            {
                (Source: healthCheckSource, Path: "apps/backend/Meridian.API/Health/ReadyHealthCheck.cs"),
                (Source: registrationSource, Path: "apps/backend/Meridian.API/Health/HealthRegistration.cs")
            },
            new MER0012RegisterHealthChecksAnalyzer(),
            assemblyName: "Meridian.API");

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportUnrelatedIHealthCheckInterfaceAsync()
    {
        const string source = """
namespace Acme.Diagnostics;

public interface IHealthCheck
{
}

public sealed class ReadyHealthCheck : IHealthCheck
{
}
""";

        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0012RegisterHealthChecksAnalyzer(),
            "apps/backend/Meridian.API/Health/ReadyHealthCheck.cs",
            assemblyName: "Meridian.API");

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportPublicLibraryHealthCheckRegisteredByHostAsync()
    {
        const string source = """
namespace Microsoft.Extensions.Diagnostics.HealthChecks;

public interface IHealthCheck
{
}

public sealed class ReadyHealthCheck : IHealthCheck
{
}
""";

        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0012RegisterHealthChecksAnalyzer(),
            "apps/backend/Meridian.Infrastructure/Health/ReadyHealthCheck.cs",
            assemblyName: "Meridian.Infrastructure");

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ReportsInternalLibraryHealthCheckWithoutLocalRegistrationAsync()
    {
        const string source = """
namespace Microsoft.Extensions.Diagnostics.HealthChecks;

public interface IHealthCheck
{
}

internal sealed class ReadyHealthCheck : IHealthCheck
{
}
""";

        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0012RegisterHealthChecksAnalyzer(),
            "apps/backend/Meridian.Infrastructure/Health/ReadyHealthCheck.cs",
            assemblyName: "Meridian.Infrastructure");

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0012RegisterHealthChecksAnalyzer.DiagnosticId);
    }
}
