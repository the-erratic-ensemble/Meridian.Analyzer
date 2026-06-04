using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0012RegisterHealthChecksAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0012";

    private const string HealthCheckInterfaceName = "IHealthCheck";
    private const string HealthCheckInterfaceNamespace = "Microsoft.Extensions.Diagnostics.HealthChecks";
    private const string HostAssemblyName = "Meridian.API";

    private static readonly LocalizableString Title = "Register IHealthCheck implementations";
    private static readonly LocalizableString MessageFormat = "IHealthCheck implementations should have a matching AddHealthChecks registration";
    private static readonly LocalizableString Description =
        "Health-check implementations without matching registrations are dead code and create misleading operational coverage.";

    internal static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        MeridianDiagnosticCategories.Reliability,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description,
        customTags: ["CompilationEnd"]);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(RegisterCompilationActions);
    }

    private static void RegisterCompilationActions(CompilationStartAnalysisContext context)
    {
        var healthCheckTypes = new ConcurrentBag<INamedTypeSymbol>();
        var registeredTypes = new ConcurrentBag<ITypeSymbol>();

        context.RegisterSymbolAction(symbolContext => {
            if (symbolContext.Symbol is INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: false } namedType &&
                ShouldTrackHealthCheck(symbolContext.Compilation, namedType) &&
                namedType.AllInterfaces.Any(IsHealthCheckInterface))
            {
                healthCheckTypes.Add(namedType);
            }
        }, SymbolKind.NamedType);

        context.RegisterSyntaxNodeAction(syntaxContext => {
            if (syntaxContext.Node is not InvocationExpressionSyntax invocation ||
                invocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
                memberAccess.Name is not GenericNameSyntax genericName ||
                genericName.TypeArgumentList.Arguments.Count == 0)
            {
                return;
            }

            if (genericName.Identifier.ValueText is not ("AddCheck" or "AddTypeActivatedCheck"))
            {
                return;
            }

            var typeInfo = syntaxContext.SemanticModel.GetSymbolInfo(genericName.TypeArgumentList.Arguments[0], syntaxContext.CancellationToken);
            if (typeInfo.Symbol is ITypeSymbol registeredType)
            {
                registeredTypes.Add(registeredType);
            }
        }, SyntaxKind.InvocationExpression);

        context.RegisterCompilationEndAction(endContext => {
            foreach (var healthCheckType in healthCheckTypes)
            {
                if (registeredTypes.Any(registeredType => SymbolEqualityComparer.Default.Equals(registeredType, healthCheckType)))
                {
                    continue;
                }

                var location = healthCheckType.Locations.FirstOrDefault(location => location.IsInSource);
                if (location is null)
                {
                    continue;
                }

                endContext.ReportDiagnostic(Diagnostic.Create(Rule, location));
            }
        });
    }

    private static bool IsHealthCheckInterface(INamedTypeSymbol interfaceType)
    {
        return string.Equals(interfaceType.Name, HealthCheckInterfaceName, StringComparison.Ordinal) &&
               string.Equals(interfaceType.ContainingNamespace.ToDisplayString(), HealthCheckInterfaceNamespace, StringComparison.Ordinal);
    }

    private static bool ShouldTrackHealthCheck(Compilation compilation, INamedTypeSymbol namedType)
    {
        return string.Equals(compilation.AssemblyName, HostAssemblyName, StringComparison.Ordinal) ||
               namedType.DeclaredAccessibility != Accessibility.Public;
    }
}
