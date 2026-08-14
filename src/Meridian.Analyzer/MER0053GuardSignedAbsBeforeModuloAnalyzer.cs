using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0053GuardSignedAbsBeforeModuloAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0053";

    private static readonly LocalizableString Title = "Guard signed Math.Abs before modulo";

    private static readonly LocalizableString MessageFormat =
        "Math.Abs can overflow for the minimum signed value before this modulo operation";

    private static readonly LocalizableString Description =
        "Math.Abs of a signed integral minimum value throws before the remainder can be calculated.";

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
            !IsSignedMathAbs(context, invocation) ||
            !IsLeftSideOfModulo(invocation))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    private static bool IsSignedMathAbs(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation)
    {
        var method = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
        return method?.Name == "Abs" &&
               method.Parameters.Length == 1 &&
               method.ContainingType is
               {
                   Name: "Math",
                   ContainingNamespace: { } containingNamespace
               } &&
               containingNamespace.ToDisplayString() == "System" &&
               IsSignedIntegral(method.Parameters[0].Type);
    }

    private static bool IsLeftSideOfModulo(InvocationExpressionSyntax invocation)
    {
        var parent = invocation.Parent;
        while (parent is ParenthesizedExpressionSyntax or CastExpressionSyntax)
            parent = parent.Parent;

        return parent is BinaryExpressionSyntax binary &&
               binary.IsKind(SyntaxKind.ModuloExpression) &&
               MeridianAnalyzerSemanticHelpers.Unwrap(binary.Left) == invocation;
    }

    private static bool IsSignedIntegral(ITypeSymbol type)
    {
        return type.SpecialType is SpecialType.System_SByte or SpecialType.System_Int16 or
            SpecialType.System_Int32 or SpecialType.System_Int64;
    }
}
