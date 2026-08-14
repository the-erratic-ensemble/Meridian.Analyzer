using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0055RequireExplicitBinaryByteOrderAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0055";

    private static readonly LocalizableString Title = "State binary numeric byte order";

    private static readonly LocalizableString MessageFormat =
        "Use an explicit little-endian or big-endian conversion instead of BitConverter's machine byte order";

    private static readonly LocalizableString Description =
        "BitConverter numeric conversions use the host machine byte order unless the surrounding code selects an explicit order.";

    private static readonly string[] NumericToMethods =
    {
        "ToChar",
        "ToInt16",
        "ToUInt16",
        "ToInt32",
        "ToUInt32",
        "ToInt64",
        "ToUInt64",
        "ToSingle",
        "ToDouble"
    };

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
            !IsByteOrderSensitiveConversion(context, invocation) ||
            IsInsideByteOrderBranch(context, invocation))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    private static bool IsByteOrderSensitiveConversion(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation)
    {
        var method = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
        if (method?.ContainingType is not
            {
                Name: "BitConverter",
                ContainingNamespace: { } containingNamespace
            } || containingNamespace.ToDisplayString() != "System")
            return false;

        return method.Name switch
        {
            "GetBytes" => method.Parameters.Length == 1 && IsMultiByteNumeric(method.Parameters[0].Type),
            "TryWriteBytes" => method.Parameters.Length == 2 &&
                IsByteSpan(method.Parameters[0].Type) &&
                IsMultiByteNumeric(method.Parameters[1].Type),
            _ => NumericToMethods.Contains(method.Name, StringComparer.Ordinal) &&
                method.Parameters.Length > 0 &&
                IsByteSequence(method.Parameters[0].Type)
        };
    }

    private static bool IsInsideByteOrderBranch(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation)
    {
        foreach (var ancestor in invocation.Ancestors())
        {
            if (ancestor is IfStatementSyntax ifStatement &&
                !ifStatement.Condition.Span.Contains(invocation.Span) &&
                ContainsEndianMarker(context, ifStatement.Condition) &&
                HasExplicitByteOrderPaths(context, ifStatement.Statement, ifStatement.Else?.Statement))
                return true;

            if (ancestor is ConditionalExpressionSyntax conditional &&
                !conditional.Condition.Span.Contains(invocation.Span) &&
                ContainsEndianMarker(context, conditional.Condition) &&
                HasExplicitByteOrderPaths(context, conditional.WhenTrue, conditional.WhenFalse))
                return true;
        }

        return false;
    }

    private static bool HasExplicitByteOrderPaths(
        SyntaxNodeAnalysisContext context,
        SyntaxNode firstPath,
        SyntaxNode? secondPath)
    {
        return secondPath is not null &&
               ContainsExplicitByteOrderOperation(context, firstPath) &&
               ContainsExplicitByteOrderOperation(context, secondPath);
    }

    private static bool ContainsExplicitByteOrderOperation(
        SyntaxNodeAnalysisContext context,
        SyntaxNode node)
    {
        return node.DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .Any(invocation =>
                IsByteOrderSensitiveConversion(context, invocation) ||
                IsExplicitEndianConversion(context, invocation));
    }

    private static bool IsExplicitEndianConversion(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation)
    {
        var method = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
        if (method?.ContainingType is not
            {
                Name: "BinaryPrimitives",
                ContainingNamespace: { } containingNamespace
            } || containingNamespace.ToDisplayString() != "System.Buffers.Binary")
            return false;

        return method.Name.IndexOf("Endian", StringComparison.Ordinal) >= 0 ||
               method.Name == "ReverseEndianness";
    }

    private static bool ContainsEndianMarker(
        SyntaxNodeAnalysisContext context,
        SyntaxNode node)
    {
        return node.DescendantNodesAndSelf()
            .OfType<MemberAccessExpressionSyntax>()
            .Any(memberAccess =>
            {
                if (memberAccess.Name.Identifier.ValueText != "IsLittleEndian" ||
                    context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken).Symbol is not
                    IFieldSymbol field)
                    return false;

                return field.IsStatic &&
                       field.ContainingType?.Name == "BitConverter" &&
                       field.ContainingType.ContainingNamespace?.ToDisplayString() == "System";
            });
    }

    private static bool IsMultiByteNumeric(ITypeSymbol type)
    {
        return type.SpecialType is SpecialType.System_Char or SpecialType.System_Int16 or
            SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32 or
            SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Single or
            SpecialType.System_Double;
    }

    private static bool IsByteSequence(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol arrayType)
            return arrayType.ElementType.SpecialType == SpecialType.System_Byte;

        return type is INamedTypeSymbol namedType &&
               namedType.Name == "ReadOnlySpan" &&
               namedType.ContainingNamespace?.ToDisplayString() == "System" &&
               namedType.TypeArguments.Length == 1 &&
               namedType.TypeArguments[0].SpecialType == SpecialType.System_Byte;
    }

    private static bool IsByteSpan(ITypeSymbol type)
    {
        return type is INamedTypeSymbol namedType &&
               namedType.Name == "Span" &&
               namedType.ContainingNamespace?.ToDisplayString() == "System" &&
               namedType.TypeArguments.Length == 1 &&
               namedType.TypeArguments[0].SpecialType == SpecialType.System_Byte;
    }
}
