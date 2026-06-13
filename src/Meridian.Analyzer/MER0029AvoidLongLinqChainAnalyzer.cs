using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0029AvoidLongLinqChainAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0029";

    private const int MinimumChainedInvocationCount = 8;

    private static readonly LocalizableString Title = "Avoid overly long LINQ or EF fluent chains";
    private static readonly LocalizableString MessageFormat =
        "Review this {0}-call LINQ or EF chain; extract named steps or intermediate locals";
    private static readonly LocalizableString Description =
        "Long LINQ and EF fluent chains are hard to review because filtering, shaping, ordering, and materialization all blur together in one expression. " +
        "Split longer chains into named intermediate query steps.";

    internal static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        MeridianDiagnosticCategories.Readability,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, Microsoft.CodeAnalysis.CSharp.SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        if (!IsQueryChainInvocation(context, invocation))
        {
            return;
        }

        if (TryGetParentChainedInvocation(invocation, out var parentInvocation) &&
            IsQueryChainInvocation(context, parentInvocation))
        {
            return;
        }

        var chainLength = CountQueryChainInvocations(context, invocation);
        if (chainLength < MinimumChainedInvocationCount)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), chainLength));
    }

    private static int CountQueryChainInvocations(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation)
    {
        var count = 0;
        InvocationExpressionSyntax? current = invocation;

        while (current is not null && IsQueryChainInvocation(context, current))
        {
            count++;
            current = GetReceiverInvocation(current);
        }

        if (count > 0 && GetInnermostQueryChainInvocation(context, invocation) is { } innermostInvocation &&
            IsAsNoTrackingInvocation(innermostInvocation))
        {
            count--;
        }

        return count;
    }

    private static bool IsQueryChainInvocation(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        var receiverType = context.SemanticModel.GetTypeInfo(memberAccess.Expression, context.CancellationToken).Type;
        var invocationTypeInfo = context.SemanticModel.GetTypeInfo(invocation, context.CancellationToken);
        var invocationType = invocationTypeInfo.Type ?? invocationTypeInfo.ConvertedType;
        return IsQueryLikeType(receiverType) || IsQueryLikeType(invocationType);
    }

    private static bool TryGetParentChainedInvocation(
        InvocationExpressionSyntax invocation,
        out InvocationExpressionSyntax parentInvocation)
    {
        parentInvocation = null!;

        if (invocation.Parent is not MemberAccessExpressionSyntax memberAccess ||
            memberAccess.Parent is not InvocationExpressionSyntax parent)
        {
            return false;
        }

        parentInvocation = parent;
        return true;
    }

    private static InvocationExpressionSyntax? GetReceiverInvocation(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression is MemberAccessExpressionSyntax { Expression: InvocationExpressionSyntax receiverInvocation }
            ? receiverInvocation
            : null;
    }

    private static InvocationExpressionSyntax? GetInnermostQueryChainInvocation(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation)
    {
        InvocationExpressionSyntax? current = invocation;
        InvocationExpressionSyntax? innermost = null;

        while (current is not null && IsQueryChainInvocation(context, current))
        {
            innermost = current;
            current = GetReceiverInvocation(current);
        }

        return innermost;
    }

    private static bool IsAsNoTrackingInvocation(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
               string.Equals(memberAccess.Name.Identifier.ValueText, "AsNoTracking", StringComparison.Ordinal);
    }

    private static bool IsQueryLikeType(ITypeSymbol? typeSymbol)
    {
        if (typeSymbol is null || typeSymbol.SpecialType == SpecialType.System_String)
        {
            return false;
        }

        if (MatchesQueryLikeType(typeSymbol))
        {
            return true;
        }

        return typeSymbol.AllInterfaces.Any(MatchesQueryLikeType);
    }

    private static bool MatchesQueryLikeType(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is not INamedTypeSymbol namedType)
        {
            return false;
        }

        var namespaceName = namedType.ContainingNamespace?.ToDisplayString();
        return (namespaceName, namedType.MetadataName) switch
        {
            ("System.Linq", "IQueryable`1") => true,
            ("System.Linq", "IOrderedQueryable`1") => true,
            ("System.Linq", "IOrderedEnumerable`1") => true,
            ("System.Collections.Generic", "IEnumerable`1") => true,
            ("System.Collections.Generic", "IAsyncEnumerable`1") => true,
            _ => false
        };
    }
}
