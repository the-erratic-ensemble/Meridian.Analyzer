using FluentAssertions;
using Xunit;

namespace Meridian.Analyzers.Tests;

public sealed class MER0022ContainRedisKeyspaceScansAnalyzerTests
{
    [Fact]
    public async Task ReportsDirectRedisKeysCallAsync()
    {
        const string source = """
namespace StackExchange.Redis
{
    public interface IServer
    {
        object Keys(string pattern);
    }
}

namespace Meridian.Infrastructure.Caching
{
using StackExchange.Redis;

public sealed class RedisCacheService
{
    public object Read(IServer server)
    {
        return server.Keys(pattern: "cache:*");
    }
}
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == MER0022ContainRedisKeyspaceScansAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportCentralRedisKeyspaceScannerAsync()
    {
        const string source = """
namespace StackExchange.Redis
{
    public interface IServer
    {
        object Keys(string pattern);
    }
}

namespace Meridian.Infrastructure.Caching
{
using StackExchange.Redis;

public sealed class RedisKeyspaceScanner
{
    public object Read(IServer server)
    {
        return server.Keys(pattern: "cache:*");
    }
}
}
""";

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportUnrelatedKeysCallAsync()
    {
        const string source = """
public sealed class CacheKeyCatalog
{
    public object Keys(string pattern)
    {
        return new object();
    }
}

public sealed class RedisCacheService
{
    public object Read(CacheKeyCatalog catalog)
    {
        return catalog.Keys(pattern: "cache:*");
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
            new MER0022ContainRedisKeyspaceScansAnalyzer(),
            "apps/backend/Meridian.Infrastructure/Caching/RedisCacheService.cs");
    }
}
