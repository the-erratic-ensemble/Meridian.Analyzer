using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0023OwnDetachedRuntimeTasksAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0023";

    private static readonly LocalizableString Title = "Own detached runtime tasks";

    private static readonly LocalizableString MessageFormat =
        "Observe this task through await, return, Task.WhenAll, or an IBackgroundTaskOwner";

    private static readonly LocalizableString Description =
        "Discarded task-returning calls and unobserved Task.Run work can hide failures and outlive their intended runtime boundary.";

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
        context.RegisterSyntaxNodeAction(AnalyzeAssignment, SyntaxKind.SimpleAssignmentExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation ||
            IsExcludedLocation(invocation) ||
            !IsTaskRun(invocation, context.SemanticModel, context.CancellationToken) ||
            IsTaskObserved(invocation, context.SemanticModel, context.CancellationToken))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not AssignmentExpressionSyntax assignment ||
            IsExcludedLocation(assignment) ||
            assignment.Left is not IdentifierNameSyntax identifierName ||
            identifierName.Identifier.ValueText != "_" ||
            assignment.Right is not InvocationExpressionSyntax invocation ||
            IsTaskRun(invocation, context.SemanticModel, context.CancellationToken) ||
            !ReturnsTaskLike(invocation, context.SemanticModel, context.CancellationToken))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, assignment.GetLocation()));
    }

    private static bool IsTaskObserved(
        InvocationExpressionSyntax taskRun,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        foreach (var ancestor in taskRun.Ancestors())
        {
            switch (ancestor)
            {
                case AwaitExpressionSyntax:
                case ReturnStatementSyntax:
                    return true;
                case ArrowExpressionClauseSyntax arrowExpressionClause
                    when arrowExpressionClause.Parent is MethodDeclarationSyntax or LocalFunctionStatementSyntax:
                    return true;
                case InvocationExpressionSyntax selectionInvocation
                    when IsTaskSelection(selectionInvocation, semanticModel, cancellationToken):
                    return false;
                case InvocationExpressionSyntax invocation
                    when IsTaskAggregation(invocation, semanticModel, cancellationToken) ||
                         IsBackgroundTaskOwnerInvocation(invocation, semanticModel, cancellationToken):
                    return true;
                case VariableDeclaratorSyntax variableDeclarator:
                    return IsDeclaredTaskObserved(
                        variableDeclarator,
                        semanticModel,
                        cancellationToken);
                case AssignmentExpressionSyntax assignment:
                    return IsAssignedTaskObserved(
                        assignment,
                        semanticModel,
                        cancellationToken);
            }
        }

        return false;
    }

    private static bool IsDeclaredTaskObserved(
        VariableDeclaratorSyntax declaration,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var localSymbol = semanticModel.GetDeclaredSymbol(declaration, cancellationToken);
        var containingMethod = MeridianAnalyzerRuleHelpers.GetContainingMethod(declaration);
        if (localSymbol is null || containingMethod is null) return false;

        return containingMethod.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(identifier => identifier.SpanStart > declaration.SpanStart)
            .Any(identifier =>
                SymbolEqualityComparer.Default.Equals(
                    semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol,
                    localSymbol) &&
                IsTaskReferenceObserved(identifier, semanticModel, cancellationToken));
    }

    private static bool IsAssignedTaskObserved(
        AssignmentExpressionSyntax assignment,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (assignment.Left is not IdentifierNameSyntax identifierName ||
            identifierName.Identifier.ValueText == "_")
            return false;

        var assignedSymbol = semanticModel.GetSymbolInfo(identifierName, cancellationToken).Symbol;
        var containingMethod = MeridianAnalyzerRuleHelpers.GetContainingMethod(assignment);
        if (assignedSymbol is not ILocalSymbol || containingMethod is null) return false;

        return containingMethod.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(identifier => identifier.SpanStart > assignment.SpanStart)
            .Any(identifier =>
                SymbolEqualityComparer.Default.Equals(
                    semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol,
                    assignedSymbol) &&
                IsTaskReferenceObserved(identifier, semanticModel, cancellationToken));
    }

    private static bool IsTaskReferenceObserved(
        IdentifierNameSyntax identifier,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        foreach (var ancestor in identifier.Ancestors())
        {
            switch (ancestor)
            {
                case AwaitExpressionSyntax:
                case ReturnStatementSyntax:
                    return true;
                case InvocationExpressionSyntax selectionInvocation
                    when IsTaskSelection(selectionInvocation, semanticModel, cancellationToken):
                    return false;
                case InvocationExpressionSyntax invocation
                    when IsTaskAggregation(invocation, semanticModel, cancellationToken) ||
                         IsBackgroundTaskOwnerInvocation(invocation, semanticModel, cancellationToken):
                    return true;
                case StatementSyntax:
                    return false;
            }
        }

        return false;
    }

    private static bool IsTaskRun(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var method = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
        return method?.Name == "Run" &&
               method.ContainingType.Name == "Task" &&
               method.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks";
    }

    private static bool IsTaskAggregation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var method = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
        return method?.Name == "WhenAll" &&
               method.ContainingType.Name == "Task" &&
               method.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks";
    }

    private static bool IsTaskSelection(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var method = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
        return method?.Name == "WhenAny" &&
               method.ContainingType.Name == "Task" &&
               method.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks";
    }

    private static bool IsBackgroundTaskOwnerInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var method = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
        var containingType = method?.ContainingType;
        return containingType?.Name == "IBackgroundTaskOwner" ||
               containingType?.AllInterfaces.Any(
                   implementedInterface => implementedInterface.Name == "IBackgroundTaskOwner") == true;
    }

    private static bool ReturnsTaskLike(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var type = semanticModel.GetTypeInfo(invocation, cancellationToken).Type as INamedTypeSymbol;
        if (type is null ||
            type.ContainingNamespace.ToDisplayString() != "System.Threading.Tasks")
            return false;

        return type.Name is "Task" or "ValueTask";
    }

    private static bool IsExcludedLocation(SyntaxNode node)
    {
        return MeridianAnalyzerRuleHelpers.IsTestPath(node.SyntaxTree.FilePath);
    }
}
