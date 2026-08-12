using FluentAssertions;
using Xunit;

namespace Meridian.Analyzer.Tests;

public sealed class MER0032AvoidInlineConditionalRecordRewritesAnalyzerTests
{
    [Fact]
    public async Task ReportsConditionalSelectThatRewritesMultipleChildCollectionsAsync()
    {
        const string source = """
using System;
using System.Collections.Generic;
using System.Linq;

public sealed record TeamSheet(
    string ClubId,
    IReadOnlyList<string> StartingFootballerIds,
    IReadOnlyList<RoleAssignment> RoleAssignments);

public sealed record RoleAssignment(string FootballerId, string RoleId);

public static class Sample
{
    public static TeamSheet[] Apply(
        IReadOnlyList<TeamSheet> sheets,
        string managedClubId,
        string offFootballerId,
        string onFootballerId,
        string roleId)
    {
        return sheets
            .Select(sheet => string.Equals(sheet.ClubId, managedClubId, StringComparison.Ordinal)
                ? sheet with {
                    StartingFootballerIds = sheet.StartingFootballerIds
                        .Select(footballerId => string.Equals(footballerId, offFootballerId, StringComparison.Ordinal)
                            ? onFootballerId
                            : footballerId)
                        .ToArray(),
                    RoleAssignments = sheet.RoleAssignments
                        .Where(assignment => !string.Equals(assignment.FootballerId, offFootballerId, StringComparison.Ordinal))
                        .Append(new RoleAssignment(onFootballerId, roleId))
                        .ToArray(),
                }
                : sheet)
            .ToArray();
    }
}
""";

        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0032AvoidInlineConditionalRecordRewritesAnalyzer());

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0032AvoidInlineConditionalRecordRewritesAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsConditionalSelectThatRewritesMultipleScalarMembersAsync()
    {
        const string source = """
using System;
using System.Collections.Generic;
using System.Linq;

public sealed record Footballer(string ClubId, string AvailabilityStatus, DateOnly? UnavailableUntil, string? AvailabilityReason);

public static class Sample
{
    public static Footballer[] Apply(IReadOnlyList<Footballer> footballers, string clubId)
    {
        return footballers
            .Select(footballer => string.Equals(footballer.ClubId, clubId, StringComparison.Ordinal)
                ? footballer with {
                    AvailabilityStatus = "available",
                    UnavailableUntil = null,
                    AvailabilityReason = null,
                }
                : footballer)
            .ToArray();
    }
}
""";

        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0032AvoidInlineConditionalRecordRewritesAnalyzer());

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0032AvoidInlineConditionalRecordRewritesAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsConditionalSelectThatRewritesFalseBranchAsync()
    {
        const string source = """
using System.Collections.Generic;
using System.Linq;

public sealed record Club(string Id, int CashBalance, int Reputation);

public static class Sample
{
    public static Club[] Apply(IReadOnlyList<Club> clubs, string clubId)
    {
        return clubs
            .Select(club => club.Id == clubId
                ? club
                : club with {
                    CashBalance = 0,
                    Reputation = 1,
                })
            .ToArray();
    }
}
""";

        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0032AvoidInlineConditionalRecordRewritesAnalyzer());

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0032AvoidInlineConditionalRecordRewritesAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsConditionalSelectThatRewritesNestedRecordMemberAsync()
    {
        const string source = """
using System;
using System.Collections.Generic;
using System.Linq;

public sealed record Finance(int CashBalance);
public sealed record Club(string Id, Finance? Finance);

public static class Sample
{
    public static Club[] Apply(IReadOnlyList<Club> clubs, string clubId, int compensationAmount, bool subtract)
    {
        return clubs
            .Select(club => club.Id == clubId && club.Finance is not null
                ? club with {
                    Finance = club.Finance with {
                        CashBalance = subtract
                            ? Math.Max(0, club.Finance.CashBalance - compensationAmount)
                            : club.Finance.CashBalance + compensationAmount,
                    },
                }
                : club)
            .ToArray();
    }
}
""";

        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0032AvoidInlineConditionalRecordRewritesAnalyzer());

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0032AvoidInlineConditionalRecordRewritesAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsConditionalSelectThatChoosesBetweenNestedRecordRewritesAsync()
    {
        const string source = """
using System;
using System.Collections.Generic;
using System.Linq;

public sealed record Finance(int CashBalance);
public sealed record Club(string Id, Finance? Finance);

public static class Sample
{
    public static Club[] Apply(
        IReadOnlyList<Club> clubs,
        string formerClubId,
        string destinationClubId,
        int compensationAmount)
    {
        return clubs
            .Select(club => club.Id == formerClubId
                ? club with {
                    Finance = club.Finance! with {
                        CashBalance = club.Finance!.CashBalance + compensationAmount,
                    },
                }
                : club.Id == destinationClubId
                    ? club with {
                        Finance = club.Finance! with {
                            CashBalance = Math.Max(0, club.Finance!.CashBalance - compensationAmount),
                        },
                    }
                    : club)
            .ToArray();
    }
}
""";

        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0032AvoidInlineConditionalRecordRewritesAnalyzer());

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0032AvoidInlineConditionalRecordRewritesAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportSimpleConditionalRecordUpdateAsync()
    {
        const string source = """
using System;
using System.Collections.Generic;
using System.Linq;

public sealed record Item(string Id, string Status);

public static class Sample
{
    public static Item[] Apply(IReadOnlyList<Item> items, string id)
    {
        return items
            .Select(item => string.Equals(item.Id, id, StringComparison.Ordinal)
                ? item with { Status = "done" }
                : item)
            .ToArray();
    }
}
""";

        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0032AvoidInlineConditionalRecordRewritesAnalyzer());

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportSingleChildCollectionRewriteAsync()
    {
        const string source = """
using System;
using System.Collections.Generic;
using System.Linq;

public sealed record TeamSheet(string ClubId, IReadOnlyList<string> StartingFootballerIds);

public static class Sample
{
    public static TeamSheet[] Apply(
        IReadOnlyList<TeamSheet> sheets,
        string managedClubId,
        string offFootballerId,
        string onFootballerId)
    {
        return sheets
            .Select(sheet => string.Equals(sheet.ClubId, managedClubId, StringComparison.Ordinal)
                ? sheet with {
                    StartingFootballerIds = sheet.StartingFootballerIds
                        .Select(footballerId => string.Equals(footballerId, offFootballerId, StringComparison.Ordinal)
                            ? onFootballerId
                            : footballerId)
                        .ToArray(),
                }
                : sheet)
            .ToArray();
    }
}
""";

        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0032AvoidInlineConditionalRecordRewritesAnalyzer());

        diagnostics.Should().BeEmpty();
    }
}
