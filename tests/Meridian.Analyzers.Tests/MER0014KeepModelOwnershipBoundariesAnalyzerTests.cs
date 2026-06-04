using FluentAssertions;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Meridian.Analyzers.Tests;

public sealed class MER0014KeepModelOwnershipBoundariesAnalyzerTests
{
    [Fact]
    public async Task ReportsEntityOutsideDatabaseEntityBoundaryAsync()
    {
        const string source = """
namespace Meridian.Core.Models;

public sealed class ReportEntity
{
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "apps/backend/Meridian.Core/Models/ReportEntity.cs");

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0014KeepModelOwnershipBoundariesAnalyzer.DiagnosticId);
    }

    [Fact]
    public void UsesInventorySeverityUntilSharedContractClassificationCompletes()
    {
        new MER0014KeepModelOwnershipBoundariesAnalyzer()
            .SupportedDiagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.Id == MER0014KeepModelOwnershipBoundariesAnalyzer.DiagnosticId)
            .Which
            .DefaultSeverity
            .Should()
            .Be(DiagnosticSeverity.Info);
    }

    [Fact]
    public async Task DoesNotReportEntityInsideDatabaseEntityBoundaryAsync()
    {
        const string source = """
namespace Meridian.Shared.Database.Entities;

public sealed class ReportEntity
{
}
""";

        var diagnostics = await GetDiagnosticsAsync(source, "apps/backend/Meridian.Shared/Database/Entities/ReportEntity.cs");

        diagnostics.Should().BeEmpty();
    }

    private static async Task<IReadOnlyCollection<Microsoft.CodeAnalysis.Diagnostic>> GetDiagnosticsAsync(
        string source,
        string path)
    {
        return await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0014KeepModelOwnershipBoundariesAnalyzer(),
            path);
    }
}
