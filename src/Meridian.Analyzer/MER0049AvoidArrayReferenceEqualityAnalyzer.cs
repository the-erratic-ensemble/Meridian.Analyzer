using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0049AvoidArrayReferenceEqualityAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0049";

    private static readonly LocalizableString Title = "Make array equality semantics explicit";

    private static readonly LocalizableString MessageFormat =
        "Compare array contents explicitly or use ReferenceEquals for array identity";

    private static readonly LocalizableString Description =
        "Array equality operators compare references, which can mistake equal contents for different values.";

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
        context.RegisterSyntaxNodeAction(AnalyzeEquality, SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression);
    }

    private static void AnalyzeEquality(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not BinaryExpressionSyntax binary ||
            MeridianAnalyzerRuleHelpers.IsTestPath(binary.SyntaxTree.FilePath) ||
            IsNullLiteral(binary.Left) ||
            IsNullLiteral(binary.Right))
            return;

        var leftType = context.SemanticModel.GetTypeInfo(binary.Left, context.CancellationToken).ConvertedType;
        var rightType = context.SemanticModel.GetTypeInfo(binary.Right, context.CancellationToken).ConvertedType;
        if (leftType is IArrayTypeSymbol && rightType is IArrayTypeSymbol)
            context.ReportDiagnostic(Diagnostic.Create(Rule, binary.OperatorToken.GetLocation()));
    }

    private static bool IsNullLiteral(ExpressionSyntax expression)
    {
        return MeridianAnalyzerSemanticHelpers.Unwrap(expression) is LiteralExpressionSyntax literal &&
               literal.IsKind(SyntaxKind.NullLiteralExpression);
    }
}
