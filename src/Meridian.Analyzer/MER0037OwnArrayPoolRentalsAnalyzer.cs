using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0037OwnArrayPoolRentalsAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0037";

    private static readonly LocalizableString Title = "Own ArrayPool rentals";

    private static readonly LocalizableString MessageFormat =
        "Return this ArrayPool rental exactly once or transfer it to a disposable rental owner";

    private static readonly LocalizableString Description =
        "Every ArrayPool rental needs a matching return to the same pool or an explicit disposable ownership transfer.";

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
        if (context.Node is not InvocationExpressionSyntax rentInvocation ||
            MeridianAnalyzerRuleHelpers.IsTestPath(rentInvocation.SyntaxTree.FilePath) ||
            !IsArrayPoolRent(context, rentInvocation))
            return;

        if (IsTransferredToRentalOwner(context, rentInvocation) ||
            IsConstructorOwnedRental(context, rentInvocation)) return;

        var local = GetAssignedLocal(context, rentInvocation);
        if (local is null ||
            rentInvocation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault() is not
            MethodDeclarationSyntax containingMethod)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, rentInvocation.GetLocation()));
            return;
        }

        var nextAssignment = containingMethod.DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .Where(assignment => assignment.SpanStart > rentInvocation.SpanStart)
            .Where(assignment => AssignmentTargetsLocal(context, assignment.Left, local))
            .OrderBy(assignment => assignment.SpanStart)
            .FirstOrDefault();

        var returnCalls = containingMethod.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => invocation.SpanStart > rentInvocation.SpanStart)
            .Where(invocation => nextAssignment is null || invocation.SpanStart < nextAssignment.SpanStart)
            .Where(invocation => IsArrayPoolReturn(context, invocation))
            .Where(invocation => HasRentalArgument(context, invocation, local))
            .Where(invocation => HasSamePoolReceiver(context, rentInvocation, invocation))
            .ToArray();

        if (returnCalls.Length == 1) return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, rentInvocation.GetLocation()));
    }

    private static bool IsArrayPoolRent(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation)
    {
        var method = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
        return method?.Name == "Rent" &&
               string.Equals(method.ContainingType?.Name, "ArrayPool", StringComparison.Ordinal) &&
               string.Equals(method.ContainingNamespace?.ToDisplayString(), "System.Buffers",
                   StringComparison.Ordinal);
    }

    private static bool IsConstructorOwnedRental(
        SyntaxNodeAnalysisContext context, InvocationExpressionSyntax rent)
    {
        if (rent.Parent is not AssignmentExpressionSyntax assignment || assignment.Right != rent ||
            assignment.Parent is not ExpressionStatementSyntax statement ||
            statement.Parent is not BlockSyntax { Parent: ConstructorDeclarationSyntax constructor } body ||
            body.Statements.LastOrDefault() != statement ||
            context.SemanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol is not
                IFieldSymbol { IsStatic: false } field ||
            rent.Expression is not MemberAccessExpressionSyntax rentAccess ||
            context.SemanticModel.GetSymbolInfo(rentAccess.Expression, context.CancellationToken).Symbol is not
                IParameterSymbol poolParameter ||
            context.SemanticModel.GetDeclaredSymbol(constructor, context.CancellationToken) is not { } ownerConstructor ||
            !SymbolEqualityComparer.Default.Equals(field.ContainingType, ownerConstructor.ContainingType) ||
            assignment.Left is not IdentifierNameSyntax and not MemberAccessExpressionSyntax
                { Expression: ThisExpressionSyntax })
            return false;

        var poolFields = MeridianResourceOwnershipHelpers.GetStoredFields(
                poolParameter, context.SemanticModel, context.CancellationToken)
            .Where(pool => pool.IsReadOnly).ToArray();
        foreach (var disposal in MeridianResourceOwnershipHelpers.GetDisposalMethods(
                     ownerConstructor.ContainingType, context.CancellationToken))
        {
            var model = context.SemanticModel;
            if (disposal.SyntaxTree != model.SyntaxTree) continue;
            if (disposal.Body is not { Statements.Count: 2 } disposeBody ||
                disposeBody.Statements[0] is not LocalDeclarationStatementSyntax localDeclaration ||
                localDeclaration.Declaration.Variables.Count != 1 ||
                localDeclaration.Declaration.Variables[0] is not { Initializer.Value: { } take } local ||
                !MeridianResourceOwnershipHelpers.IsAtomicTake(take, field, model, context.CancellationToken) ||
                disposeBody.Statements[1] is not IfStatementSyntax { Else: null } guard ||
                guard.Condition is not IsPatternExpressionSyntax
                {
                    Pattern: UnaryPatternSyntax
                    {
                        RawKind: (int)SyntaxKind.NotPattern,
                        Pattern: ConstantPatternSyntax { Expression.RawKind: (int)SyntaxKind.NullLiteralExpression }
                    }
                } condition ||
                !SymbolEqualityComparer.Default.Equals(
                    model.GetSymbolInfo(condition.Expression, context.CancellationToken).Symbol,
                    model.GetDeclaredSymbol(local, context.CancellationToken)))
                continue;

            var returnStatement = guard.Statement is BlockSyntax { Statements.Count: 1 } block
                ? block.Statements[0] : guard.Statement;
            if (returnStatement is not ExpressionStatementSyntax
                {
                    Expression: InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } returned
                } ||
                model.GetSymbolInfo(returned, context.CancellationToken).Symbol is not
                    IMethodSymbol { Name: "Return" } returnMethod ||
                !MeridianAnalyzerSemanticHelpers.IsTypeOrDerivedFrom(
                    returnMethod.ContainingType, "System.Buffers", "ArrayPool") ||
                !poolFields.Any(pool => SymbolEqualityComparer.Default.Equals(
                    model.GetSymbolInfo(access.Expression, context.CancellationToken).Symbol, pool)) ||
                returned.ArgumentList.Arguments.FirstOrDefault() is not { } rentalArgument ||
                !SymbolEqualityComparer.Default.Equals(
                    model.GetSymbolInfo(rentalArgument.Expression, context.CancellationToken).Symbol,
                    model.GetDeclaredSymbol(local, context.CancellationToken)))
                continue;

            return true;
        }

        return false;
    }

    private static bool IsArrayPoolReturn(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation)
    {
        var method = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
        return method?.Name == "Return" &&
               string.Equals(method.ContainingType?.Name, "ArrayPool", StringComparison.Ordinal) &&
               string.Equals(method.ContainingNamespace?.ToDisplayString(), "System.Buffers",
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

    private static bool AssignmentTargetsLocal(
        SyntaxNodeAnalysisContext context,
        ExpressionSyntax left,
        ILocalSymbol local)
    {
        return left is IdentifierNameSyntax identifier &&
               SymbolEqualityComparer.Default.Equals(
                   context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol,
                   local);
    }

    private static bool HasRentalArgument(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax returnInvocation,
        ILocalSymbol local)
    {
        var argument = returnInvocation.ArgumentList.Arguments.FirstOrDefault();
        return argument is not null &&
               argument.Expression is IdentifierNameSyntax identifier &&
               SymbolEqualityComparer.Default.Equals(
                   context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol,
                   local);
    }

    private static bool HasSamePoolReceiver(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax rentInvocation,
        InvocationExpressionSyntax returnInvocation)
    {
        if (rentInvocation.Expression is not MemberAccessExpressionSyntax rentMember ||
            returnInvocation.Expression is not MemberAccessExpressionSyntax returnMember)
            return false;

        return MeridianAnalyzerSemanticHelpers.IsSameReference(
            rentMember.Expression,
            returnMember.Expression,
            context.SemanticModel,
            context.CancellationToken);
    }

    private static bool IsTransferredToRentalOwner(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax rentInvocation)
    {
        var objectCreation = rentInvocation.Ancestors()
            .OfType<ObjectCreationExpressionSyntax>()
            .FirstOrDefault(creation => creation.ArgumentList?.Arguments.Any(argument =>
                argument.Expression.Span.Contains(rentInvocation.Span)) == true);
        if (objectCreation is null) return false;

        var type = context.SemanticModel.GetTypeInfo(objectCreation, context.CancellationToken).Type;
        return type is INamedTypeSymbol namedType &&
               namedType.Name.EndsWith("Rental", StringComparison.Ordinal) &&
               (MeridianAnalyzerSemanticHelpers.Implements(type, "System", "IDisposable") ||
                MeridianAnalyzerSemanticHelpers.Implements(type, "System", "IAsyncDisposable"));
    }
}
