using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzers.Tests;

internal static class AnalyzerTestHost
{
    internal static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
        string source,
        DiagnosticAnalyzer analyzer,
        string path = "Test0.cs",
        string assemblyName = "Meridian.Analyzers.Tests.Generated")
    {
        return await GetDiagnosticsAsync(
            new[] { (Source: source, Path: path) },
            analyzer,
            assemblyName);
    }

    internal static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
        IReadOnlyCollection<(string Source, string Path)> sources,
        DiagnosticAnalyzer analyzer,
        string assemblyName = "Meridian.Analyzers.Tests.Generated")
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var syntaxTrees = sources
            .Select(source => CSharpSyntaxTree.ParseText(source.Source, parseOptions, source.Path))
            .ToArray();
        var references = GetMetadataReferences();
        var compilation = CSharpCompilation.Create(
            assemblyName: assemblyName,
            syntaxTrees: syntaxTrees,
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create(analyzer)).GetAnalyzerDiagnosticsAsync();

        return diagnostics.OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start).ToImmutableArray();
    }

    private static ImmutableArray<MetadataReference> GetMetadataReferences()
    {
        var assemblies = new[]
        {
            typeof(object).Assembly,
            typeof(Enumerable).Assembly,
            typeof(System.Linq.Expressions.Expression).Assembly,
            typeof(Task).Assembly,
            typeof(System.Runtime.GCSettings).Assembly,
            Assembly.Load("System.Runtime")
        };

        return assemblies
            .DistinctBy(assembly => assembly.Location)
            .Select<Assembly, MetadataReference>(assembly => MetadataReference.CreateFromFile(assembly.Location))
            .ToImmutableArray();
    }
}
