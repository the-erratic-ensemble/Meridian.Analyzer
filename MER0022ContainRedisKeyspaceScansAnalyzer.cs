using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0022ContainRedisKeyspaceScansAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0022";

    private const string RedisServerTypeName = "IServer";
    private const string RedisServerTypeNamespace = "StackExchange.Redis";

    private static readonly LocalizableString Title = "Contain Redis keyspace scans";
    private static readonly LocalizableString MessageFormat = "Route Redis keyspace scans through an approved bounded helper";
    private static readonly LocalizableString Description =
        "Direct IServer.Keys scans are operationally expensive and easy to duplicate. Runtime code should use a single bounded helper with cancellation, batching, and logging policy.";

    internal static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        MeridianDiagnosticCategories.Performance,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation ||
            MeridianAnalyzerRuleHelpers.IsTestPath(invocation.SyntaxTree.FilePath))
        {
            return;
        }

        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
            string.Equals(memberAccess.Name.Identifier.ValueText, "Keys", StringComparison.Ordinal) &&
            !IsApprovedKeyspaceBoundary(invocation) &&
            IsRedisKeysInvocation(context, invocation, memberAccess))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
        }
    }

    private static bool IsApprovedKeyspaceBoundary(SyntaxNode node)
    {
        var containingClass = MeridianAnalyzerRuleHelpers.GetContainingClass(node);
        var className = containingClass?.Identifier.ValueText ?? string.Empty;

        return className.Contains("RedisKeyspace", StringComparison.Ordinal) ||
               className.Contains("RedisKeyScanner", StringComparison.Ordinal);
    }

    private static bool IsRedisKeysInvocation(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax memberAccess)
    {
        var receiverType = context.SemanticModel.GetTypeInfo(memberAccess.Expression, context.CancellationToken).Type;
        if (IsRedisServerType(receiverType) ||
            receiverType?.AllInterfaces.Any(IsRedisServerType) == true)
        {
            return true;
        }

        return context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is IMethodSymbol methodSymbol &&
               IsRedisServerType(methodSymbol.ContainingType);
    }

    private static bool IsRedisServerType(ITypeSymbol? typeSymbol)
    {
        return typeSymbol is INamedTypeSymbol namedType &&
               string.Equals(namedType.Name, RedisServerTypeName, StringComparison.Ordinal) &&
               string.Equals(namedType.ContainingNamespace.ToDisplayString(), RedisServerTypeNamespace, StringComparison.Ordinal);
    }
}
