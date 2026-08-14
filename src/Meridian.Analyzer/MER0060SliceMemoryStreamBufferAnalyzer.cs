using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0060SliceMemoryStreamBufferAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0060";

    private static readonly LocalizableString Title = "Slice a MemoryStream buffer to its written range";

    private static readonly LocalizableString MessageFormat =
        "Apply an explicit written range before this MemoryStream buffer escapes";

    private static readonly LocalizableString Description =
        "MemoryStream.GetBuffer can expose capacity beyond the stream length when its backing array is passed as a complete payload.";

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
        context.RegisterSyntaxNodeAction(AnalyzeGetBuffer, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeGetBuffer(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax getBuffer ||
            MeridianAnalyzerRuleHelpers.IsTestPath(getBuffer.SyntaxTree.FilePath) ||
            !IsMemoryStreamGetBuffer(context, getBuffer) ||
            IsExplicitRangeUse(context, getBuffer) ||
            IsNonEscapingInspection(getBuffer))
            return;

        var local = GetAssignedLocal(context, getBuffer);
        if (local is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, getBuffer.GetLocation()));
            return;
        }

        var containingMethod = getBuffer.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (containingMethod is null)
            return;

        foreach (var identifier in containingMethod.DescendantNodes()
                     .OfType<IdentifierNameSyntax>()
                     .Where(identifier => identifier.SpanStart > getBuffer.Span.End)
                     .Where(identifier => SymbolEqualityComparer.Default.Equals(
                         context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol,
                         local))
                     .OrderBy(identifier => identifier.SpanStart))
        {
            if (identifier.Parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Name == identifier)
                continue;

            if (IsExplicitRangeUse(context, identifier) || IsNonEscapingInspection(identifier))
                continue;

            if (identifier.Parent is AssignmentExpressionSyntax assignment &&
                assignment.Left == identifier &&
                assignment.IsKind(SyntaxKind.SimpleAssignmentExpression))
                return;

            if (identifier.Parent is ArgumentSyntax argument &&
                argument.Parent is ArgumentListSyntax argumentList &&
                argumentList.Parent is InvocationExpressionSyntax invocation &&
                !ReceivesByteArray(context, invocation, argument))
                continue;

            context.ReportDiagnostic(Diagnostic.Create(Rule, identifier.GetLocation()));
            return;
        }
    }

    private static bool IsMemoryStreamGetBuffer(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation)
    {
        var method = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
        return method?.Name == "GetBuffer" &&
               method.Parameters.Length == 0 &&
               method.ContainingType?.Name == "MemoryStream" &&
               method.ContainingType.ContainingNamespace?.ToDisplayString() == "System.IO";
    }

    private static ILocalSymbol? GetAssignedLocal(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation)
    {
        foreach (var ancestor in invocation.Ancestors())
        {
            if (ancestor is VariableDeclaratorSyntax declaration &&
                declaration.Initializer?.Value.Span.Contains(invocation.Span) == true)
                return context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) as ILocalSymbol;

            if (ancestor is AssignmentExpressionSyntax assignment &&
                assignment.Right.Span.Contains(invocation.Span) &&
                assignment.Left is IdentifierNameSyntax identifier)
                return context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol as ILocalSymbol;

            if (ancestor is MethodDeclarationSyntax)
                break;
        }

        return null;
    }

    private static bool IsExplicitRangeUse(
        SyntaxNodeAnalysisContext context,
        SyntaxNode node)
    {
        foreach (var invocation in node.AncestorsAndSelf().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
                memberAccess.Name.Identifier.ValueText is not ("AsSpan" or "AsMemory") ||
                invocation.ArgumentList.Arguments.Count < 2 ||
                (!memberAccess.Expression.Span.Contains(node.Span) &&
                 !invocation.ArgumentList.Arguments.Any(argument => argument.Expression.Span.Contains(node.Span))))
                continue;

            var method = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
            if (method?.ContainingNamespace?.ToDisplayString() == "System" &&
                method.ContainingType?.Name == "MemoryExtensions")
                return true;
        }

        foreach (var objectCreation in node.AncestorsAndSelf().OfType<ObjectCreationExpressionSyntax>())
        {
            if (objectCreation.ArgumentList is null ||
                !objectCreation.ArgumentList.Arguments.Any(argument => argument.Expression.Span.Contains(node.Span)) ||
                objectCreation.ArgumentList.Arguments.Count < 3 ||
                context.SemanticModel.GetTypeInfo(objectCreation, context.CancellationToken).Type is not
                INamedTypeSymbol type)
                continue;

            if (type.ContainingNamespace?.ToDisplayString() == "System" &&
                type.Name is "ArraySegment" or "Span" or "ReadOnlySpan" or "Memory" or "ReadOnlyMemory")
                return true;
        }

        return false;
    }

    private static bool IsNonEscapingInspection(SyntaxNode node)
    {
        if (node.Parent is ElementAccessExpressionSyntax elementAccess &&
            elementAccess.Expression == node)
            return true;

        return node.Parent is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Expression == node &&
               memberAccess.Parent is not InvocationExpressionSyntax &&
               memberAccess.Name.Identifier.ValueText is "Length" or "LongLength" or "Rank";
    }

    private static bool ReceivesByteArray(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        ArgumentSyntax argument)
    {
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method ||
            argument.Parent is not ArgumentListSyntax argumentList)
            return false;

        var parameter = argument.NameColon is not null
            ? method.Parameters.FirstOrDefault(parameter =>
                parameter.Name == argument.NameColon.Name.Identifier.ValueText)
            : method.Parameters.ElementAtOrDefault(argumentList.Arguments.IndexOf(argument));

        return parameter?.Type is IArrayTypeSymbol arrayType &&
               arrayType.ElementType.SpecialType == SpecialType.System_Byte;
    }
}
