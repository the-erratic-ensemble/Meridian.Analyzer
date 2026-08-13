using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0039BindDatabaseCommandsToActiveTransactionAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0039";

    private static readonly LocalizableString Title = "Bind database commands to the active transaction";

    private static readonly LocalizableString MessageFormat =
        "Bind this database command to the active transaction before execution";

    private static readonly LocalizableString Description =
        "A command created from a connection with an active transaction must carry that transaction before execution.";

    private static readonly string[] ExecutionMethodNames =
    {
        "ExecuteReader",
        "ExecuteReaderAsync",
        "ExecuteScalar",
        "ExecuteScalarAsync",
        "ExecuteNonQuery",
        "ExecuteNonQueryAsync"
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
        if (context.Node is not InvocationExpressionSyntax executionInvocation ||
            MeridianAnalyzerRuleHelpers.IsTestPath(executionInvocation.SyntaxTree.FilePath) ||
            executionInvocation.Expression is not MemberAccessExpressionSyntax executionMember ||
            context.SemanticModel.GetSymbolInfo(executionInvocation, context.CancellationToken).Symbol is not
            IMethodSymbol executionMethod ||
            !ExecutionMethodNames.Contains(executionMethod.Name, StringComparer.Ordinal) ||
            !MeridianAnalyzerSemanticHelpers.IsTypeOrDerivedFrom(
                executionMethod.ContainingType,
                "System.Data.Common",
                "DbCommand") ||
            executionInvocation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault() is not
            MethodDeclarationSyntax containingMethod)
            return;

        var commandConnection = FindCommandConnection(
            context,
            executionMember.Expression,
            containingMethod,
            executionInvocation.SpanStart);
        if (commandConnection is null) return;

        var transactions = containingMethod.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => invocation.SpanStart < executionInvocation.SpanStart)
            .Where(invocation => IsBeginTransaction(context, invocation))
            .Select(invocation => CreateTransactionInfo(context, invocation))
            .Where(info => info is not null)
            .Cast<TransactionInfo>()
            .Where(info => ConnectionsMatch(
                context,
                info,
                commandConnection))
            .ToArray();
        if (transactions.Length == 0) return;

        if (transactions.Any(transaction => HasTransactionBinding(
                context,
                containingMethod,
                executionMember.Expression,
                transaction.Transaction,
                executionInvocation.SpanStart)))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, executionInvocation.GetLocation()));
    }

    private static bool IsBeginTransaction(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation)
    {
        var method = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
        return method?.Name.StartsWith("BeginTransaction", StringComparison.Ordinal) == true &&
               MeridianAnalyzerSemanticHelpers.IsTypeOrDerivedFrom(
                   method.ContainingType,
                   "System.Data.Common",
                   "DbConnection") &&
               MeridianAnalyzerSemanticHelpers.IsTypeOrDerivedFrom(
                   method.ReturnType,
                   "System.Data.Common",
                   "DbTransaction");
    }

    private static TransactionInfo? CreateTransactionInfo(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
            GetAssignedLocal(context, invocation) is not ILocalSymbol transaction)
            return null;

        return new TransactionInfo(transaction, memberAccess.Expression);
    }

    private static ExpressionSyntax? FindCommandConnection(
        SyntaxNodeAnalysisContext context,
        ExpressionSyntax commandExpression,
        MethodDeclarationSyntax containingMethod,
        int usePosition)
    {
        commandExpression = MeridianAnalyzerSemanticHelpers.Unwrap(commandExpression);

        if (commandExpression is InvocationExpressionSyntax directCreation &&
            IsCreateCommand(context, directCreation) &&
            directCreation.Expression is MemberAccessExpressionSyntax directMember)
            return directMember.Expression;

        var commandSymbol = MeridianAnalyzerSemanticHelpers.GetReferencedSymbol(
            commandExpression,
            context.SemanticModel,
            context.CancellationToken);
        if (commandSymbol is null) return null;

        var initializers = containingMethod.DescendantNodes()
            .OfType<VariableDeclaratorSyntax>()
            .Where(declaration => declaration.SpanStart < usePosition)
            .Where(declaration => SymbolEqualityComparer.Default.Equals(
                context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken),
                commandSymbol))
            .Select(declaration => declaration.Initializer?.Value)
            .Where(value => value is not null)
            .Cast<ExpressionSyntax>()
            .OrderByDescending(value => value.SpanStart);

        foreach (var initializer in initializers)
        {
            if (initializer is InvocationExpressionSyntax creation &&
                IsCreateCommand(context, creation) &&
                creation.Expression is MemberAccessExpressionSyntax memberAccess)
                return memberAccess.Expression;
        }

        return containingMethod.DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .Where(assignment => assignment.SpanStart < usePosition)
            .Where(assignment => MeridianAnalyzerSemanticHelpers.GetReferencedSymbol(
                assignment.Left,
                context.SemanticModel,
                context.CancellationToken) is ISymbol leftSymbol &&
                                 SymbolEqualityComparer.Default.Equals(leftSymbol, commandSymbol))
            .OrderByDescending(assignment => assignment.SpanStart)
            .Select(assignment => assignment.Right)
            .OfType<InvocationExpressionSyntax>()
            .Where(creation => IsCreateCommand(context, creation))
            .Select(creation => creation.Expression as MemberAccessExpressionSyntax)
            .Where(memberAccess => memberAccess is not null)
            .Select(memberAccess => memberAccess!.Expression)
            .FirstOrDefault();
    }

    private static bool IsCreateCommand(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation)
    {
        var method = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
        return method?.Name == "CreateCommand" &&
               MeridianAnalyzerSemanticHelpers.IsTypeOrDerivedFrom(
                   method.ContainingType,
                   "System.Data.Common",
                   "DbConnection") &&
               MeridianAnalyzerSemanticHelpers.IsTypeOrDerivedFrom(
                   method.ReturnType,
                   "System.Data.Common",
                   "DbCommand");
    }

    private static bool ConnectionsMatch(
        SyntaxNodeAnalysisContext context,
        TransactionInfo transaction,
        ExpressionSyntax commandConnection)
    {
        if (MeridianAnalyzerSemanticHelpers.IsSameReference(
                transaction.Connection,
                commandConnection,
                context.SemanticModel,
                context.CancellationToken))
            return true;

        commandConnection = MeridianAnalyzerSemanticHelpers.Unwrap(commandConnection);
        if (commandConnection is not MemberAccessExpressionSyntax memberAccess ||
            !string.Equals(memberAccess.Name.Identifier.ValueText, "Connection", StringComparison.Ordinal))
            return false;

        return SymbolEqualityComparer.Default.Equals(
            MeridianAnalyzerSemanticHelpers.GetReferencedSymbol(
                memberAccess.Expression,
                context.SemanticModel,
                context.CancellationToken),
            transaction.Transaction);
    }

    private static bool HasTransactionBinding(
        SyntaxNodeAnalysisContext context,
        MethodDeclarationSyntax containingMethod,
        ExpressionSyntax commandExpression,
        ILocalSymbol transaction,
        int executionPosition)
    {
        return containingMethod.DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .Where(assignment => assignment.SpanStart < executionPosition)
            .Where(assignment => assignment.Left is MemberAccessExpressionSyntax memberAccess &&
                                 string.Equals(memberAccess.Name.Identifier.ValueText, "Transaction",
                                     StringComparison.Ordinal) &&
                                 MeridianAnalyzerSemanticHelpers.IsSameReference(
                                     commandExpression,
                                     memberAccess.Expression,
                                     context.SemanticModel,
                                     context.CancellationToken))
            .Any(assignment => SymbolEqualityComparer.Default.Equals(
                MeridianAnalyzerSemanticHelpers.GetReferencedSymbol(
                    assignment.Right,
                    context.SemanticModel,
                    context.CancellationToken),
                transaction));
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

    private sealed class TransactionInfo
    {
        internal TransactionInfo(ILocalSymbol transaction, ExpressionSyntax connection)
        {
            Transaction = transaction;
            Connection = connection;
        }

        internal ILocalSymbol Transaction { get; }

        internal ExpressionSyntax Connection { get; }
    }
}
