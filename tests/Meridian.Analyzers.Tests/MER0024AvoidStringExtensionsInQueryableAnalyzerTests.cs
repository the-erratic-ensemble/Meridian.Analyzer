using FluentAssertions;
using Xunit;

namespace Meridian.Analyzers.Tests;

public sealed class MER0024AvoidStringExtensionsInQueryableAnalyzerTests {
    [Fact]
    public async Task ReportsDiagnostic_WhenStringExtensionIsUsedInsideQueryableWherePredicateAsync() {
        const string source = """
using System.Linq;
using Meridian.Shared.Extensions;

namespace Meridian.Shared.Extensions {
    public static class StringExtensions {
        public static bool IsNullOrWhiteSpace(this string? value) => string.IsNullOrWhiteSpace(value);
    }
}

public sealed class Entity {
    public string? Name { get; set; }
}

public sealed class Repo {
    public IQueryable<Entity> Filter(IQueryable<Entity> query) {
        return query.Where(e => !e.Name.IsNullOrWhiteSpace());
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0024AvoidStringExtensionsInQueryableAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsDiagnostic_WhenStringExtensionIsUsedInsideExpressionPredicateAsync() {
        const string source = """
using System;
using System.Linq.Expressions;
using Meridian.Shared.Extensions;

namespace Meridian.Shared.Extensions {
    public static class StringExtensions {
        public static bool IsNullOrEmpty(this string? value) => string.IsNullOrEmpty(value);
    }
}

public sealed class Entity {
    public string? Name { get; set; }
}

public sealed class Repo {
    public Expression<Func<Entity, bool>> BuildPredicate() {
        return e => !e.Name.IsNullOrEmpty();
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0024AvoidStringExtensionsInQueryableAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReport_WhenStringExtensionIsUsedInInMemoryLinqPredicateAsync() {
        const string source = """
using System.Collections.Generic;
using System.Linq;
using Meridian.Shared.Extensions;

namespace Meridian.Shared.Extensions {
    public static class StringExtensions {
        public static bool IsNullOrWhiteSpace(this string? value) => string.IsNullOrWhiteSpace(value);
    }
}

public sealed class Entity {
    public string? Name { get; set; }
}

public sealed class Repo {
    public IEnumerable<Entity> Filter(IEnumerable<Entity> items) {
        return items.Where(e => !e.Name.IsNullOrWhiteSpace());
    }
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    private static async Task<IReadOnlyCollection<Microsoft.CodeAnalysis.Diagnostic>> GetDiagnosticsAsync(string source) {
        return await AnalyzerTestHost.GetDiagnosticsAsync(
            source,
            new MER0024AvoidStringExtensionsInQueryableAnalyzer(),
            "apps/backend/Meridian.Infrastructure/Repositories/Repo.cs");
    }
}
