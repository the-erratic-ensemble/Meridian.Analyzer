using FluentAssertions;
using Xunit;

namespace Meridian.Analyzer.Tests;

public sealed class MER0035RequireRouteCancellationTokenAnalyzerTests
{
    [Fact]
    public async Task ReportsAsyncRouteUsingNoneWithoutCancellationParameterAsync()
    {
        const string source = """
using System.Threading;
using System.Threading.Tasks;

public sealed class JsonRpcRequest { }

public sealed class CompetitionRoutes
{
    public Task<object> HandleGetCompetitionAsync(JsonRpcRequest request)
    {
        return LoadAsync(CancellationToken.None);
    }

    private static Task<object> LoadAsync(CancellationToken cancellationToken) =>
        Task.FromResult<object>(new());
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0035RequireRouteCancellationTokenAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsNoneInsideRouteThatAlreadyAcceptsCancellationAsync()
    {
        const string source = """
using System.Threading;
using System.Threading.Tasks;

public sealed class JsonRpcRequest { }

public sealed class CompetitionRoutes
{
    public Task<object> HandleGetCompetitionAsync(
        JsonRpcRequest request,
        CancellationToken cancellationToken)
    {
        return LoadAsync(CancellationToken.None);
    }

    private static Task<object> LoadAsync(CancellationToken cancellationToken) =>
        Task.FromResult<object>(new());
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0035RequireRouteCancellationTokenAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportRouteThatForwardsCancellationAsync()
    {
        const string source = """
using System.Threading;
using System.Threading.Tasks;

public sealed class JsonRpcRequest { }

public sealed class CompetitionRoutes
{
    public Task<object> HandleGetCompetitionAsync(
        JsonRpcRequest request,
        CancellationToken cancellationToken)
    {
        return LoadAsync(cancellationToken);
    }

    private static Task<object> LoadAsync(CancellationToken cancellationToken) =>
        Task.FromResult<object>(new());
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportNonRouteCleanupUsingNoneAsync()
    {
        const string source = """
using System.Threading;
using System.Threading.Tasks;

public sealed class DatabaseSession
{
    public ValueTask DisposeAsync()
    {
        return CloseAsync(CancellationToken.None);
    }

    private static ValueTask CloseAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportRouteWithoutCancellableWorkAsync()
    {
        const string source = """
using System.Threading.Tasks;

public sealed class JsonRpcRequest { }

public sealed class CompetitionRoutes
{
    public Task<object> HandleGetCompetitionAsync(JsonRpcRequest request)
    {
        return Task.FromResult<object>(new());
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
            new MER0035RequireRouteCancellationTokenAnalyzer(),
            "src/Sidecar/Routing/CompetitionRoutes.cs");
    }
}
