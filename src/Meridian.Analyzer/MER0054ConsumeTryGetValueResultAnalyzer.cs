using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0054ConsumeTryGetValueResultAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0054";

    private static readonly LocalizableString Title = "Consume the TryGetValue result";

    private static readonly LocalizableString MessageFormat =
        "Use the TryGetValue Boolean result or state the default-on-missing policy explicitly";

    private static readonly LocalizableString Description =
        "Ignoring TryGetValue hides the difference between a missing key and a stored default value.";

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
            invocation.Parent is not ExpressionStatementSyntax expressionStatement ||
            expressionStatement.Expression != invocation ||
            !IsDictionaryTryGetValue(context, invocation) ||
            !TryGetOutSymbol(context, invocation, out var outSymbol) ||
            !HasLaterRead(context, invocation, outSymbol))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    private static bool IsDictionaryTryGetValue(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation)
    {
        var method = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
        if (method?.Name != "TryGetValue" ||
            method.ReturnType.SpecialType != SpecialType.System_Boolean ||
            method.Parameters.Length != 2 ||
            method.Parameters[1].RefKind != RefKind.Out)
            return false;

        var type = method.ContainingType;
        if (type is null)
            return false;

        if ((type.Name is "IDictionary" or "IReadOnlyDictionary" &&
             type.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic") ||
            (type.Name == "IImmutableDictionary" &&
             type.ContainingNamespace?.ToDisplayString() == "System.Collections.Immutable"))
            return true;

        var namespaceName = type.ContainingNamespace?.ToDisplayString();
        if (namespaceName is not
            ("System.Collections.Generic" or "System.Collections.Concurrent" or "System.Collections.Immutable" or
             "System.Collections.Frozen" or "System.Collections.ObjectModel"))
            return false;

        return type.Name is "Dictionary" or "SortedDictionary" or "ConcurrentDictionary" or
            "ImmutableDictionary" or "ImmutableSortedDictionary" or "ReadOnlyDictionary" or "FrozenDictionary";
    }

    private static bool TryGetOutSymbol(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        out ISymbol symbol)
    {
        symbol = null!;
        var argument = invocation.ArgumentList.Arguments.FirstOrDefault(argument =>
            argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword));
        if (argument is null)
            return false;

        symbol = argument.Expression switch
        {
            IdentifierNameSyntax identifier => context.SemanticModel.GetSymbolInfo(
                identifier,
                context.CancellationToken).Symbol!,
            DeclarationExpressionSyntax declaration when declaration.Designation is SingleVariableDesignationSyntax designation =>
                context.SemanticModel.GetDeclaredSymbol(designation, context.CancellationToken)!,
            _ => null!
        };

        return symbol is not null;
    }

    private static bool HasLaterRead(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        ISymbol outSymbol)
    {
        var containingMethod = invocation.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (containingMethod is null)
            return false;

        return containingMethod.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(identifier => identifier.SpanStart > invocation.Span.End)
            .Where(identifier => SymbolEqualityComparer.Default.Equals(
                context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol,
                outSymbol))
            .Any(IsReadReference);
    }

    private static bool IsReadReference(IdentifierNameSyntax identifier)
    {
        if (identifier.Parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Name == identifier)
            return false;

        if (identifier.Parent is ArgumentSyntax argument &&
            argument.Expression == identifier &&
            argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword))
            return false;

        if (identifier.Parent is AssignmentExpressionSyntax assignment &&
            assignment.Left == identifier &&
            assignment.IsKind(SyntaxKind.SimpleAssignmentExpression))
            return false;

        if (identifier.Parent is DeclarationExpressionSyntax)
            return false;

        return true;
    }
}
