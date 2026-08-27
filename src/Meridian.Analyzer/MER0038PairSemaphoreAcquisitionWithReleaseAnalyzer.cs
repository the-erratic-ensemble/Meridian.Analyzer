using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0038PairSemaphoreAcquisitionWithReleaseAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0038";

    private static readonly LocalizableString Title = "Pair SemaphoreSlim acquisition with release ownership";

    private static readonly LocalizableString MessageFormat =
        "Release this SemaphoreSlim acquisition in a covering finally block or transfer it to a releaser owner";

    private static readonly LocalizableString Description =
        "Every successful SemaphoreSlim wait must return its capacity exactly once through a visible release owner.";

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
        if (context.Node is not InvocationExpressionSyntax waitInvocation ||
            MeridianAnalyzerRuleHelpers.IsTestPath(waitInvocation.SyntaxTree.FilePath) ||
            !TryGetSuccessfulWait(context, waitInvocation, out var waitReceiver) ||
            waitInvocation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault() is not
            MethodDeclarationSyntax containingMethod)
            return;

        if (HasReleaserTransfer(context, waitInvocation, containingMethod)) return;

        var releases = containingMethod.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => invocation.SpanStart > waitInvocation.SpanStart)
            .Where(invocation => IsSemaphoreRelease(context, invocation, waitReceiver))
            .ToArray();

        if (releases.Length == 1 && IsCoveringFinally(waitInvocation, releases[0])) return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, waitInvocation.GetLocation()));
    }

    private static bool TryGetSuccessfulWait(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        out ExpressionSyntax receiver)
    {
        receiver = null!;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return false;

        var method = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
        if (method is null ||
            method.Name is not ("Wait" or "WaitAsync") ||
            !MeridianAnalyzerSemanticHelpers.IsTypeOrDerivedFrom(
                method.ContainingType,
                "System.Threading",
                "SemaphoreSlim"))
            return false;

        if (method.Name == "WaitAsync")
        {
            var returnType = method.ReturnType as INamedTypeSymbol;
            if (returnType?.TypeArguments.Length != 0 ||
                invocation.Ancestors().OfType<AwaitExpressionSyntax>().FirstOrDefault() is null)
                return false;
        }
        else if (!method.ReturnsVoid)
        {
            return false;
        }

        receiver = memberAccess.Expression;
        return true;
    }

    private static bool IsSemaphoreRelease(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        ExpressionSyntax waitReceiver)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
            !string.Equals(memberAccess.Name.Identifier.ValueText, "Release", StringComparison.Ordinal))
            return false;

        var method = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
        return method is not null &&
               MeridianAnalyzerSemanticHelpers.IsTypeOrDerivedFrom(
                   method.ContainingType,
                   "System.Threading",
                   "SemaphoreSlim") &&
               MeridianAnalyzerSemanticHelpers.IsSameReference(
                   waitReceiver,
                   memberAccess.Expression,
                   context.SemanticModel,
                   context.CancellationToken);
    }

    private static bool IsCoveringFinally(
        InvocationExpressionSyntax waitInvocation,
        InvocationExpressionSyntax releaseInvocation)
    {
        var finallyClause = releaseInvocation.Ancestors().OfType<FinallyClauseSyntax>().FirstOrDefault();
        if (finallyClause?.Parent is not TryStatementSyntax tryStatement) return false;

        if (tryStatement.Block.Span.Contains(waitInvocation.Span)) return true;
        return waitInvocation.SpanStart < tryStatement.SpanStart;
    }

    private static bool HasReleaserTransfer(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax waitInvocation,
        MethodDeclarationSyntax containingMethod)
    {
        return containingMethod.DescendantNodes()
            .OfType<BaseObjectCreationExpressionSyntax>()
            .Where(objectCreation => objectCreation.SpanStart > waitInvocation.SpanStart)
            .Any(objectCreation =>
            {
                if (context.SemanticModel.GetTypeInfo(objectCreation, context.CancellationToken).Type is not
                    INamedTypeSymbol type ||
                    (!type.Name.EndsWith("Lease", StringComparison.Ordinal) &&
                     !type.Name.EndsWith("Releaser", StringComparison.Ordinal)) ||
                    (!MeridianAnalyzerSemanticHelpers.Implements(type, "System", "IDisposable") &&
                     !MeridianAnalyzerSemanticHelpers.Implements(type, "System", "IAsyncDisposable")))
                    return false;

                if (objectCreation.Ancestors().OfType<ReturnStatementSyntax>().Any()) return true;

                if (objectCreation.Ancestors().OfType<VariableDeclaratorSyntax>().FirstOrDefault() is not
                    VariableDeclaratorSyntax declaration)
                    return false;

                var local = context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken);
                return local is not null && containingMethod.DescendantNodes()
                    .OfType<ReturnStatementSyntax>()
                    .Where(statement => statement.SpanStart > objectCreation.SpanStart)
                    .Any(statement => statement.Expression is IdentifierNameSyntax identifier &&
                                      SymbolEqualityComparer.Default.Equals(
                                          context.SemanticModel.GetSymbolInfo(
                                              identifier,
                                              context.CancellationToken).Symbol,
                                          local));
            });
    }
}
