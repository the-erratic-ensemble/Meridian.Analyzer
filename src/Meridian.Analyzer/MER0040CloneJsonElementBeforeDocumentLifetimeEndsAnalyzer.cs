using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0040CloneJsonElementBeforeDocumentLifetimeEndsAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0040";

    private static readonly LocalizableString Title = "Clone JsonElement values that outlive their JsonDocument";

    private static readonly LocalizableString MessageFormat =
        "Clone this JsonElement before it escapes the owned JsonDocument lifetime";

    private static readonly LocalizableString Description =
        "A JsonElement derived from a locally disposed JsonDocument needs Clone before returning or storing it beyond the document scope.";

    private static readonly string[] CollectionInsertionMethodNames =
    {
        "Add",
        "Insert",
        "Enqueue",
        "Push",
        "Set",
        "SetValue",
        "TryAdd"
    };

    private static readonly string[] CallbackMethodNames =
    {
        "Add",
        "ContinueWith",
        "Invoke",
        "Post",
        "Queue",
        "Register",
        "Subscribe"
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
        if (context.Node is not InvocationExpressionSyntax parseInvocation ||
            MeridianAnalyzerRuleHelpers.IsTestPath(parseInvocation.SyntaxTree.FilePath) ||
            !IsJsonDocumentParse(context, parseInvocation) ||
            GetAssignedLocal(context, parseInvocation) is not ILocalSymbol document ||
            parseInvocation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault() is not
            MethodDeclarationSyntax containingMethod ||
            !HasLocalDisposalOwnership(context, parseInvocation, document, containingMethod))
            return;

        var analysis = new DocumentAnalysis(context, containingMethod, document, parseInvocation.SpanStart);
        analysis.ReportEscapes();
    }

    private static bool IsJsonDocumentParse(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation)
    {
        var method = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
        return method?.Name == "Parse" &&
               string.Equals(method.ContainingType?.Name, "JsonDocument", StringComparison.Ordinal) &&
               string.Equals(method.ContainingNamespace?.ToDisplayString(), "System.Text.Json",
                   StringComparison.Ordinal);
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

            if (ancestor is MethodDeclarationSyntax) break;
        }

        return null;
    }

    private static bool HasLocalDisposalOwnership(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax parseInvocation,
        ILocalSymbol document,
        MethodDeclarationSyntax containingMethod)
    {
        var declaration = parseInvocation.Ancestors()
            .OfType<VariableDeclaratorSyntax>()
            .FirstOrDefault(candidate => candidate.Initializer?.Value.Span.Contains(parseInvocation.Span) == true);
        if (declaration?.Parent?.Parent is LocalDeclarationStatementSyntax localDeclaration &&
            localDeclaration.UsingKeyword.IsKind(SyntaxKind.UsingKeyword))
            return true;

        if (declaration?.Parent?.Parent is UsingStatementSyntax) return true;

        return containingMethod.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => invocation.SpanStart > parseInvocation.SpanStart)
            .Any(invocation =>
                invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name.Identifier.ValueText == "Dispose" &&
                memberAccess.Expression is IdentifierNameSyntax identifier &&
                SymbolEqualityComparer.Default.Equals(
                    MeridianAnalyzerSemanticHelpers.GetReferencedSymbol(
                        identifier,
                        context.SemanticModel,
                        context.CancellationToken),
                    document));
    }

    private sealed class DocumentAnalysis
    {
        private readonly SyntaxNodeAnalysisContext _context;
        private readonly MethodDeclarationSyntax _containingMethod;
        private readonly ILocalSymbol _document;
        private readonly int _parsePosition;
        private readonly HashSet<int> _reportedPositions = new();

        internal DocumentAnalysis(
            SyntaxNodeAnalysisContext context,
            MethodDeclarationSyntax containingMethod,
            ILocalSymbol document,
            int parsePosition)
        {
            _context = context;
            _containingMethod = containingMethod;
            _document = document;
            _parsePosition = parsePosition;
        }

        internal void ReportEscapes()
        {
            if (DocumentIsTransferred()) return;

            foreach (var statement in _containingMethod.DescendantNodes().OfType<ReturnStatementSyntax>()
                         .Where(statement => statement.SpanStart > _parsePosition))
            {
                if (statement.Expression is { } expression && ContainsEscapingElement(expression))
                    Report(expression);
            }

            foreach (var assignment in _containingMethod.DescendantNodes()
                         .OfType<AssignmentExpressionSyntax>()
                         .Where(assignment => assignment.SpanStart > _parsePosition)
                         .Where(IsMemberOrElementAssignment))
            {
                if (ContainsEscapingElement(assignment.Right)) Report(assignment.Right);
            }

            foreach (var invocation in _containingMethod.DescendantNodes()
                         .OfType<InvocationExpressionSyntax>()
                         .Where(invocation => invocation.SpanStart > _parsePosition)
                         .Where(IsCollectionInsertion))
            {
                foreach (var argument in invocation.ArgumentList.Arguments)
                    if (ContainsEscapingElement(argument.Expression))
                        Report(argument.Expression);
            }

            foreach (var invocation in _containingMethod.DescendantNodes()
                         .OfType<InvocationExpressionSyntax>()
                         .Where(invocation => invocation.SpanStart > _parsePosition)
                         .Where(IsCallbackRegistration))
            {
                foreach (var argument in invocation.ArgumentList.Arguments)
                {
                    if (argument.Expression is not LambdaExpressionSyntax lambda ||
                        !LambdaCapturesElement(lambda))
                        continue;

                    Report(lambda);
                }
            }
        }

        private bool DocumentIsTransferred()
        {
            if (_containingMethod.DescendantNodes().OfType<ReturnStatementSyntax>()
                .Where(statement => statement.SpanStart > _parsePosition)
                .Any(statement => statement.Expression is { } expression && IsDocumentReference(expression)))
                return true;

            return _containingMethod.DescendantNodes().OfType<AssignmentExpressionSyntax>()
                .Where(assignment => assignment.SpanStart > _parsePosition)
                .Where(assignment => assignment.Left is MemberAccessExpressionSyntax)
                .Any(assignment => IsDocumentReference(assignment.Right));
        }

        private bool IsMemberOrElementAssignment(AssignmentExpressionSyntax assignment)
        {
            return assignment.Left is ElementAccessExpressionSyntax ||
                   MeridianAnalyzerSemanticHelpers.GetReferencedSymbol(
                       assignment.Left,
                       _context.SemanticModel,
                       _context.CancellationToken) is IFieldSymbol or IPropertySymbol;
        }

        private bool IsCollectionInsertion(InvocationExpressionSyntax invocation)
        {
            return invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                   CollectionInsertionMethodNames.Contains(
                       memberAccess.Name.Identifier.ValueText,
                       StringComparer.Ordinal);
        }

        private bool IsCallbackRegistration(InvocationExpressionSyntax invocation)
        {
            return invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                   CallbackMethodNames.Contains(memberAccess.Name.Identifier.ValueText, StringComparer.Ordinal) &&
                   invocation.ArgumentList.Arguments.Any(argument => argument.Expression is LambdaExpressionSyntax);
        }

        private bool LambdaCapturesElement(LambdaExpressionSyntax lambda)
        {
            return lambda.DescendantNodes().OfType<IdentifierNameSyntax>()
                .Where(identifier => !IsInsideClone(identifier))
                .Any(identifier => IsElementDerived(identifier, new HashSet<ISymbol>(SymbolEqualityComparer.Default), 0));
        }

        private bool ContainsEscapingElement(ExpressionSyntax expression)
        {
            expression = MeridianAnalyzerSemanticHelpers.Unwrap(expression);
            if (IsCloneExpression(expression)) return false;
            if (IsElementDerived(expression, new HashSet<ISymbol>(SymbolEqualityComparer.Default), 0)) return true;

            return expression switch
            {
                ConditionalExpressionSyntax conditional =>
                    ContainsEscapingElement(conditional.WhenTrue) ||
                    ContainsEscapingElement(conditional.WhenFalse),
                ObjectCreationExpressionSyntax objectCreation =>
                    objectCreation.ArgumentList?.Arguments.Any(argument =>
                        ContainsEscapingElement(argument.Expression)) == true ||
                    objectCreation.Initializer?.Expressions.Any(initializer =>
                        initializer is AssignmentExpressionSyntax assignment &&
                        ContainsEscapingElement(assignment.Right)) == true,
                ArrayCreationExpressionSyntax arrayCreation =>
                    arrayCreation.Initializer?.Expressions.Any(ContainsEscapingElement) == true,
                ImplicitArrayCreationExpressionSyntax arrayCreation =>
                    arrayCreation.Initializer.Expressions.Any(ContainsEscapingElement),
                IdentifierNameSyntax identifier => LocalInitializerContainsElement(identifier),
                _ => false
            };
        }

        private bool LocalInitializerContainsElement(IdentifierNameSyntax identifier)
        {
            var symbol = MeridianAnalyzerSemanticHelpers.GetReferencedSymbol(
                identifier,
                _context.SemanticModel,
                _context.CancellationToken);
            if (symbol is not ILocalSymbol local) return false;

            var declaration = _containingMethod.DescendantNodes()
                .OfType<VariableDeclaratorSyntax>()
                .Where(candidate => candidate.SpanStart < identifier.SpanStart)
                .Where(candidate => SymbolEqualityComparer.Default.Equals(
                    _context.SemanticModel.GetDeclaredSymbol(candidate, _context.CancellationToken),
                    local))
                .OrderByDescending(candidate => candidate.SpanStart)
                .FirstOrDefault();
            return declaration?.Initializer?.Value is { } initializer &&
                   ContainsEscapingElement(initializer);
        }

        private bool IsElementDerived(
            ExpressionSyntax expression,
            HashSet<ISymbol> activeSymbols,
            int depth)
        {
            if (depth > 12) return false;
            expression = MeridianAnalyzerSemanticHelpers.Unwrap(expression);

            if (expression is ConditionalExpressionSyntax conditional)
                return IsElementDerived(conditional.WhenTrue, activeSymbols, depth + 1) ||
                       IsElementDerived(conditional.WhenFalse, activeSymbols, depth + 1);

            if (expression is ElementAccessExpressionSyntax elementAccess)
                return IsElementDerived(elementAccess.Expression, activeSymbols, depth + 1);

            if (expression is MemberAccessExpressionSyntax memberAccess)
            {
                if (memberAccess.Name.Identifier.ValueText == "RootElement" &&
                    IsDocumentReference(memberAccess.Expression))
                    return true;

                if (memberAccess.Name.Identifier.ValueText == "Value" &&
                    IsJsonPropertyDerived(memberAccess.Expression, activeSymbols, depth + 1))
                    return true;

                return false;
            }

            if (expression is InvocationExpressionSyntax invocation &&
                invocation.Expression is MemberAccessExpressionSyntax invocationMember)
            {
                if (invocationMember.Name.Identifier.ValueText == "Clone") return false;

                return invocationMember.Name.Identifier.ValueText == "GetProperty" &&
                       IsElementDerived(invocationMember.Expression, activeSymbols, depth + 1);
            }

            if (expression is not IdentifierNameSyntax identifier) return false;

            var symbol = MeridianAnalyzerSemanticHelpers.GetReferencedSymbol(
                identifier,
                _context.SemanticModel,
                _context.CancellationToken);
            if (symbol is null || SymbolEqualityComparer.Default.Equals(symbol, _document)) return false;
            if (!activeSymbols.Add(symbol)) return false;

            try
            {
                if (symbol is ILocalSymbol local)
                {
                    var declaration = _containingMethod.DescendantNodes()
                        .OfType<VariableDeclaratorSyntax>()
                        .Where(candidate => candidate.SpanStart < identifier.SpanStart)
                        .Where(candidate => SymbolEqualityComparer.Default.Equals(
                            _context.SemanticModel.GetDeclaredSymbol(candidate, _context.CancellationToken),
                            local))
                        .OrderByDescending(candidate => candidate.SpanStart)
                        .FirstOrDefault();
                    if (declaration?.Initializer?.Value is { } initializer &&
                        IsElementDerived(initializer, activeSymbols, depth + 1))
                        return true;

                    var assignment = _containingMethod.DescendantNodes()
                        .OfType<AssignmentExpressionSyntax>()
                        .Where(candidate => candidate.SpanStart < identifier.SpanStart)
                        .Where(candidate => candidate.Left is IdentifierNameSyntax left &&
                                            SymbolEqualityComparer.Default.Equals(
                                                MeridianAnalyzerSemanticHelpers.GetReferencedSymbol(
                                                    left,
                                                    _context.SemanticModel,
                                                    _context.CancellationToken),
                                                local))
                        .OrderByDescending(candidate => candidate.SpanStart)
                        .FirstOrDefault();
                    if (assignment?.Right is { } right &&
                        IsElementDerived(right, activeSymbols, depth + 1))
                        return true;

                    foreach (var statement in _containingMethod.DescendantNodes()
                                 .OfType<ForEachStatementSyntax>()
                                 .Where(statement => statement.SpanStart < identifier.SpanStart)
                                 .Where(statement => SymbolEqualityComparer.Default.Equals(
                                     _context.SemanticModel.GetDeclaredSymbol(
                                         statement,
                                         _context.CancellationToken),
                                     local)))
                    {
                        if (IsArrayEnumeration(statement.Expression, activeSymbols, depth + 1)) return true;
                    }
                }
            }
            finally
            {
                activeSymbols.Remove(symbol);
            }

            return false;
        }

        private bool IsJsonPropertyDerived(
            ExpressionSyntax expression,
            HashSet<ISymbol> activeSymbols,
            int depth)
        {
            expression = MeridianAnalyzerSemanticHelpers.Unwrap(expression);
            if (expression is not IdentifierNameSyntax identifier) return false;

            var symbol = MeridianAnalyzerSemanticHelpers.GetReferencedSymbol(
                identifier,
                _context.SemanticModel,
                _context.CancellationToken);
            return symbol is ILocalSymbol local &&
                   _containingMethod.DescendantNodes()
                       .OfType<ForEachStatementSyntax>()
                       .Where(statement => statement.SpanStart < identifier.SpanStart)
                       .Where(statement => SymbolEqualityComparer.Default.Equals(
                           _context.SemanticModel.GetDeclaredSymbol(statement, _context.CancellationToken),
                           local))
                       .Any(statement => IsObjectEnumeration(statement.Expression, activeSymbols, depth + 1));
        }

        private bool IsArrayEnumeration(
            ExpressionSyntax expression,
            HashSet<ISymbol> activeSymbols,
            int depth)
        {
            return IsEnumeration(expression, "EnumerateArray", activeSymbols, depth);
        }

        private bool IsObjectEnumeration(
            ExpressionSyntax expression,
            HashSet<ISymbol> activeSymbols,
            int depth)
        {
            return IsEnumeration(expression, "EnumerateObject", activeSymbols, depth);
        }

        private bool IsEnumeration(
            ExpressionSyntax expression,
            string methodName,
            HashSet<ISymbol> activeSymbols,
            int depth)
        {
            expression = MeridianAnalyzerSemanticHelpers.Unwrap(expression);
            return expression is InvocationExpressionSyntax invocation &&
                   invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                   memberAccess.Name.Identifier.ValueText == methodName &&
                   IsElementDerived(memberAccess.Expression, activeSymbols, depth + 1);
        }

        private bool IsDocumentReference(ExpressionSyntax expression)
        {
            expression = MeridianAnalyzerSemanticHelpers.Unwrap(expression);
            return expression is IdentifierNameSyntax identifier &&
                   SymbolEqualityComparer.Default.Equals(
                       MeridianAnalyzerSemanticHelpers.GetReferencedSymbol(
                           identifier,
                           _context.SemanticModel,
                           _context.CancellationToken),
                       _document);
        }

        private bool IsCloneExpression(ExpressionSyntax expression)
        {
            expression = MeridianAnalyzerSemanticHelpers.Unwrap(expression);
            return expression is InvocationExpressionSyntax invocation &&
                   invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                   memberAccess.Name.Identifier.ValueText == "Clone" &&
                   IsElementDerived(
                       memberAccess.Expression,
                       new HashSet<ISymbol>(SymbolEqualityComparer.Default),
                       0);
        }

        private bool IsInsideClone(IdentifierNameSyntax identifier)
        {
            return identifier.Ancestors().OfType<InvocationExpressionSyntax>()
                .Any(invocation => invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                                   memberAccess.Name.Identifier.ValueText == "Clone" &&
                                   memberAccess.Expression.Span.Contains(identifier.Span));
        }

        private void Report(ExpressionSyntax expression)
        {
            if (!_reportedPositions.Add(expression.SpanStart)) return;
            _context.ReportDiagnostic(Diagnostic.Create(Rule, expression.GetLocation()));
        }
    }
}
