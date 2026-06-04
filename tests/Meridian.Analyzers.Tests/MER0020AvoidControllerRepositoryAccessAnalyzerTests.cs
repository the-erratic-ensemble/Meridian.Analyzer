using FluentAssertions;
using Xunit;

namespace Meridian.Analyzers.Tests;

public sealed class MER0020AvoidControllerRepositoryAccessAnalyzerTests
{
    [Fact]
    public async Task ReportsRepositoryCallInsideControllerActionAsync()
    {
        const string source = """
public sealed class ReportsController : ControllerBase
{
    private readonly dynamic _reportRepository;

    [HttpGet]
    public object Get()
    {
        return _reportRepository.FindAsync();
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0020AvoidControllerRepositoryAccessAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportServiceCallInsideControllerActionAsync()
    {
        const string source = """
public sealed class ReportsController : ControllerBase
{
    private readonly dynamic _reportService;

    [HttpGet]
    public object Get()
    {
        return _reportService.GetAsync();
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ReportsTypedDbContextCallInsideControllerActionAsync()
    {
        const string source = """
public sealed class MeridianDbContext
{
    public object SaveChangesAsync()
    {
        return new object();
    }
}

public sealed class ReportsController : ControllerBase
{
    private readonly MeridianDbContext _context = new MeridianDbContext();

    [HttpPost]
    public object Save()
    {
        return _context.SaveChangesAsync();
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0020AvoidControllerRepositoryAccessAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportGenericContextReceiverNameAsync()
    {
        const string source = """
public sealed class ReportsController : ControllerBase
{
    [HttpGet]
    public object Get(dynamic context)
    {
        return context.ToListAsync();
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
            new MER0020AvoidControllerRepositoryAccessAnalyzer());
    }
}
