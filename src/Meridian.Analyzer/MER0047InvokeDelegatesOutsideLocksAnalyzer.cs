using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0047InvokeDelegatesOutsideLocksAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0047";

    private static readonly LocalizableString Title = "Invoke delegates after leaving locks";

    private static readonly LocalizableString MessageFormat =
        "Invoke this delegate or event after leaving the lock";

    private static readonly LocalizableString Description =
        "Callback code invoked while a lock is held can re-enter shared state or block the lock owner.";

    internal static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        MeridianDiagnosticCategories.Reliability,
        DiagnosticSeverity.Warning,
        true,
        Description);

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
            MeridianAnalyzerRuleHelpers.IsTestPath(invocation.SyntaxTree.FilePath) ||
            !IsInsideLockBody(invocation) ||
            !IsDelegateInvocation(context, invocation))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    private static bool IsInsideLockBody(InvocationExpressionSyntax invocation)
    {
        return invocation.Ancestors().OfType<LockStatementSyntax>()
            .Any(lockStatement =>
                lockStatement.Statement.Span.Contains(invocation.Span) &&
                !invocation.Ancestors().Any(ancestor =>
                    ancestor is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax));
    }

    private static bool IsDelegateInvocation(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation)
    {
        var receiver = GetDelegateReceiver(invocation);
        if (receiver is not null && IsDelegateType(context, receiver))
            return true;

        var invokedSymbol = context.SemanticModel.GetSymbolInfo(
            invocation.Expression,
            context.CancellationToken).Symbol;
        return invokedSymbol is IMethodSymbol method &&
               method.MethodKind == MethodKind.DelegateInvoke;
    }

    private static ExpressionSyntax? GetDelegateReceiver(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            return memberAccess.Expression;

        if (invocation.Expression is MemberBindingExpressionSyntax &&
            invocation.Parent is ConditionalAccessExpressionSyntax conditionalAccess)
            return conditionalAccess.Expression;

        return invocation.Expression;
    }

    private static bool IsDelegateType(
        SyntaxNodeAnalysisContext context,
        ExpressionSyntax expression)
    {
        var type = context.SemanticModel.GetTypeInfo(expression, context.CancellationToken).Type;
        if (type?.TypeKind == TypeKind.Delegate)
            return true;

        return context.SemanticModel.GetSymbolInfo(expression, context.CancellationToken).Symbol switch
        {
            IEventSymbol eventSymbol => eventSymbol.Type.TypeKind == TypeKind.Delegate,
            ILocalSymbol localSymbol => localSymbol.Type.TypeKind == TypeKind.Delegate,
            IFieldSymbol fieldSymbol => fieldSymbol.Type.TypeKind == TypeKind.Delegate,
            IPropertySymbol propertySymbol => propertySymbol.Type.TypeKind == TypeKind.Delegate,
            _ => false
        };
    }
}
