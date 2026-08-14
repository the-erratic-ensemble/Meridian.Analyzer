using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0052AvoidCollectionMutationDuringEnumerationAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0052";

    private static readonly LocalizableString Title = "Do not mutate an active collection enumeration";

    private static readonly LocalizableString MessageFormat =
        "Mutating this collection during its foreach enumeration invalidates the enumerator; use a snapshot or separate pass";

    private static readonly LocalizableString Description =
        "Standard collection enumerators can fail when their collection is structurally changed by the active foreach body.";

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
        context.RegisterSyntaxNodeAction(AnalyzeForEach, SyntaxKind.ForEachStatement);
    }

    private static void AnalyzeForEach(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not ForEachStatementSyntax forEach ||
            MeridianAnalyzerRuleHelpers.IsTestPath(forEach.SyntaxTree.FilePath))
            return;

        var sourceExpression = MeridianAnalyzerSemanticHelpers.Unwrap(forEach.Expression);
        var sourceSymbol = context.SemanticModel.GetSymbolInfo(sourceExpression, context.CancellationToken).Symbol;
        var sourceType = context.SemanticModel.GetTypeInfo(sourceExpression, context.CancellationToken).Type as INamedTypeSymbol;
        if (sourceSymbol is null || sourceType is null || !IsTrackedCollectionType(sourceType))
            return;

        foreach (var invocation in forEach.Statement.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
                !MeridianAnalyzerSemanticHelpers.IsSameReference(
                    sourceExpression,
                    memberAccess.Expression,
                    context.SemanticModel,
                    context.CancellationToken) ||
                !IsInvalidatingMethod(sourceType, context.SemanticModel.GetSymbolInfo(
                    invocation,
                    context.CancellationToken).Symbol as IMethodSymbol))
                continue;

            context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
        }

    }

    private static bool IsTrackedCollectionType(INamedTypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Interface)
            return false;

        var namespaceName = type.OriginalDefinition.ContainingNamespace?.ToDisplayString();
        return namespaceName switch
        {
            "System.Collections.Generic" => type.OriginalDefinition.Name is
                "Dictionary" or "HashSet" or "LinkedList" or "List" or "Queue" or "SortedDictionary" or
                "SortedSet" or "Stack",
            "System.Collections.ObjectModel" => type.OriginalDefinition.Name is "Collection" or "ObservableCollection",
            _ => false
        };
    }

    private static bool IsInvalidatingMethod(INamedTypeSymbol sourceType, IMethodSymbol? method)
    {
        if (method is null || !DeclaresMemberInCollectionHierarchy(sourceType, method.ContainingType))
            return false;

        var typeName = sourceType.OriginalDefinition.Name;
        return typeName switch
        {
            "Dictionary" or "SortedDictionary" => method.Name is "Add" or "Clear" or "Remove" or "TryAdd",
            "HashSet" or "SortedSet" => method.Name is "Add" or "Clear" or "ExceptWith" or "IntersectWith" or
                "Remove" or "RemoveWhere" or "SymmetricExceptWith" or "UnionWith",
            "LinkedList" => method.Name is "AddAfter" or "AddBefore" or "AddFirst" or "AddLast" or "Clear" or
                "Remove" or "RemoveFirst" or "RemoveLast",
            "List" => method.Name is "Add" or "AddRange" or "Clear" or "Insert" or "InsertRange" or "Remove" or
                "RemoveAll" or "RemoveAt" or "Reverse" or "Sort",
            "Queue" => method.Name is "Clear" or "Dequeue" or "Enqueue",
            "Stack" => method.Name is "Clear" or "Pop" or "Push",
            "Collection" or "ObservableCollection" => method.Name is "Add" or "Clear" or "Insert" or "Remove" or
                "RemoveAt",
            _ => false
        };
    }

    private static bool DeclaresMemberInCollectionHierarchy(
        INamedTypeSymbol sourceType,
        INamedTypeSymbol? declaringType)
    {
        for (var current = sourceType; current is not null; current = current.BaseType)
        {
            if (declaringType is not null &&
                SymbolEqualityComparer.Default.Equals(
                    current.OriginalDefinition,
                    declaringType.OriginalDefinition))
                return true;
        }

        return false;
    }

}
