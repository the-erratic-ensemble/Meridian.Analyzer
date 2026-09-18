using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0038PairSemaphoreAcquisitionWithReleaseAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0038";

    private static readonly LocalizableString Title = "Pair SemaphoreSlim acquisition with release ownership";

    private static readonly LocalizableString MessageFormat =
        "Release this SemaphoreSlim acquisition in a covering finally block or transfer it to a releaser owner";

    private static readonly LocalizableString Description =
        "Every successful SemaphoreSlim wait must return its capacity exactly once through a visible release owner.";

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
        if (context.Node is not InvocationExpressionSyntax waitInvocation ||
            MeridianAnalyzerRuleHelpers.IsTestPath(waitInvocation.SyntaxTree.FilePath) ||
            !TryGetSuccessfulWait(context, waitInvocation, out var waitReceiver) ||
            waitInvocation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault() is not
            MethodDeclarationSyntax containingMethod)
            return;

        if (HasReleaserTransfer(context, waitInvocation, waitReceiver, containingMethod)) return;

        var releases = containingMethod.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => invocation.SpanStart > waitInvocation.SpanStart)
            .Where(invocation => IsSemaphoreRelease(context, invocation, waitReceiver))
            .ToArray();

        if (releases.Length == 1 && IsCoveringFinally(waitInvocation, releases[0])) return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, waitInvocation.GetLocation()));
    }

    private static bool TryGetSuccessfulWait(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        out ExpressionSyntax receiver)
    {
        receiver = null!;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return false;

        var method = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
        if (method is null ||
            method.Name is not ("Wait" or "WaitAsync") ||
            !MeridianAnalyzerSemanticHelpers.IsTypeOrDerivedFrom(
                method.ContainingType,
                "System.Threading",
                "SemaphoreSlim"))
            return false;

        if (method.Name == "WaitAsync")
        {
            var returnType = method.ReturnType as INamedTypeSymbol;
            if (returnType?.TypeArguments.Length != 0 ||
                invocation.Ancestors().OfType<AwaitExpressionSyntax>().FirstOrDefault() is null)
                return false;
        }
        else if (!method.ReturnsVoid)
        {
            return false;
        }

        receiver = memberAccess.Expression;
        return true;
    }

    private static bool IsSemaphoreRelease(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        ExpressionSyntax waitReceiver)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
            !string.Equals(memberAccess.Name.Identifier.ValueText, "Release", StringComparison.Ordinal))
            return false;

        var method = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
        return method is not null &&
               MeridianAnalyzerSemanticHelpers.IsTypeOrDerivedFrom(
                   method.ContainingType,
                   "System.Threading",
                   "SemaphoreSlim") &&
               MeridianAnalyzerSemanticHelpers.IsSameReference(
                   waitReceiver,
                   memberAccess.Expression,
                   context.SemanticModel,
                   context.CancellationToken);
    }

    private static bool IsCoveringFinally(
        InvocationExpressionSyntax waitInvocation,
        InvocationExpressionSyntax releaseInvocation)
    {
        var finallyClause = releaseInvocation.Ancestors().OfType<FinallyClauseSyntax>().FirstOrDefault();
        if (finallyClause?.Parent is not TryStatementSyntax tryStatement) return false;

        if (tryStatement.Block.Span.Contains(waitInvocation.Span)) return true;
        return waitInvocation.SpanStart < tryStatement.SpanStart;
    }

    private static bool HasReleaserTransfer(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax waitInvocation,
        ExpressionSyntax waitReceiver,
        MethodDeclarationSyntax containingMethod)
    {
        return containingMethod.DescendantNodes()
            .OfType<BaseObjectCreationExpressionSyntax>()
            .Where(objectCreation => objectCreation.SpanStart > waitInvocation.SpanStart)
            .Any(objectCreation =>
            {
                if (context.SemanticModel.GetOperation(objectCreation, context.CancellationToken) is not
                    IObjectCreationOperation { Type: INamedTypeSymbol type } creation ||
                    !(creation.Arguments.Any(argument =>
                        argument.Parameter is { } parameter &&
                        MeridianResourceOwnershipHelpers.GetStoredFields(
                                parameter, context.SemanticModel, context.CancellationToken)
                            .Any(field =>
                                argument.Value.Syntax is ExpressionSyntax expression &&
                                MeridianAnalyzerSemanticHelpers.IsSameReference(
                                    waitReceiver, expression, context.SemanticModel, context.CancellationToken) &&
                                ReleasesStoredSemaphore(context, type, field) ||
                                argument.Value is IInstanceReferenceOperation &&
                                context.SemanticModel.GetOperation(waitReceiver, context.CancellationToken) is
                                    IFieldReferenceOperation { Instance: IInstanceReferenceOperation } waitedField &&
                                ReleasesThroughContainingOwner(context, type, field, waitedField.Field))) ||
                      ReleasesCapturedSemaphoreContainer(context, creation, type, waitReceiver)))
                    return false;

                if (objectCreation.Parent is ReturnStatementSyntax) return true;

                if (objectCreation.Ancestors().OfType<VariableDeclaratorSyntax>().FirstOrDefault() is not
                    VariableDeclaratorSyntax declaration)
                    return false;

                var local = context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken);
                return local is not null && containingMethod.DescendantNodes()
                    .OfType<ReturnStatementSyntax>()
                    .Where(statement => statement.SpanStart > objectCreation.SpanStart)
                    .Any(statement => statement.Expression is IdentifierNameSyntax identifier &&
                                      SymbolEqualityComparer.Default.Equals(
                                          context.SemanticModel.GetSymbolInfo(
                                              identifier,
                                              context.CancellationToken).Symbol,
                                          local));
            });
    }

    private static bool ReleasesStoredSemaphore(
        SyntaxNodeAnalysisContext context, INamedTypeSymbol type, IFieldSymbol field)
    {
        foreach (var disposal in MeridianResourceOwnershipHelpers.GetDisposalMethods(type, context.CancellationToken))
        {
            var model = context.SemanticModel;
            if (disposal.SyntaxTree != model.SyntaxTree) continue;
            foreach (var invocation in disposal.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (model.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not
                        IMethodSymbol { Name: "Release", Parameters.Length: 0 } method ||
                    !MeridianAnalyzerSemanticHelpers.IsTypeOrDerivedFrom(
                        method.ContainingType, "System.Threading", "SemaphoreSlim"))
                    continue;

                // Atomically detach the slot owner on the first Dispose call.
                if (invocation.Expression is MemberBindingExpressionSyntax &&
                    invocation.Parent is ConditionalAccessExpressionSyntax conditional &&
                    MeridianResourceOwnershipHelpers.IsAtomicTake(
                        conditional.Expression, field, model, context.CancellationToken) &&
                    (disposal.ExpressionBody?.Expression == conditional ||
                     conditional.Parent is ExpressionStatementSyntax { Parent: BlockSyntax block } &&
                     block == disposal.Body && block.Statements.Count == 1))
                    return true;

                // Transaction owners release capacity in their disposal finally block.
                if (field.IsReadOnly &&
                    invocation.Expression is MemberAccessExpressionSyntax access &&
                    SymbolEqualityComparer.Default.Equals(
                        model.GetSymbolInfo(access.Expression, context.CancellationToken).Symbol, field) &&
                    invocation.Parent is ExpressionStatementSyntax { Parent: BlockSyntax releaseBlock } &&
                    releaseBlock.Parent is FinallyClauseSyntax { Parent: TryStatementSyntax cleanup } &&
                    cleanup.Parent == disposal.Body)
                    return true;
            }
        }

        return false;
    }

    private static bool ReleasesThroughContainingOwner(
        SyntaxNodeAnalysisContext context, INamedTypeSymbol type, IFieldSymbol owner, IFieldSymbol semaphore)
    {
        foreach (var disposal in MeridianResourceOwnershipHelpers.GetDisposalMethods(type, context.CancellationToken))
        {
            var model = context.SemanticModel;
            if (disposal.SyntaxTree != model.SyntaxTree) continue;
            if (disposal.Body is not { Statements.Count: 3 } body ||
                body.Statements[0] is not LocalDeclarationStatementSyntax localDeclaration ||
                localDeclaration.Declaration.Variables.Count != 1 ||
                localDeclaration.Declaration.Variables[0] is not { Initializer.Value: { } take } local ||
                !MeridianResourceOwnershipHelpers.IsAtomicTake(take, owner, model, context.CancellationToken) ||
                body.Statements[1] is not ExpressionStatementSyntax
                {
                    Expression: ConditionalAccessExpressionSyntax
                    {
                        WhenNotNull: InvocationExpressionSyntax invoke
                    } conditional
                } ||
                !SymbolEqualityComparer.Default.Equals(
                    model.GetSymbolInfo(conditional.Expression, context.CancellationToken).Symbol,
                    model.GetDeclaredSymbol(local, context.CancellationToken)) ||
                model.GetSymbolInfo(invoke, context.CancellationToken).Symbol is not
                    IMethodSymbol { Parameters.Length: 0, ReturnsVoid: true } releaseMethod ||
                !SymbolEqualityComparer.Default.Equals(releaseMethod.ContainingType, semaphore.ContainingType))
                continue;

            foreach (var reference in releaseMethod.DeclaringSyntaxReferences)
            {
                if (reference.GetSyntax(context.CancellationToken) is not MethodDeclarationSyntax
                    { Body.Statements.Count: 1 } release ||
                    release.Body!.Statements[0] is not ExpressionStatementSyntax
                    { Expression: InvocationExpressionSyntax invocation })
                    continue;

                var releaseModel = context.SemanticModel;
                if (release.SyntaxTree != releaseModel.SyntaxTree) continue;
                if (releaseModel.GetOperation(invocation, context.CancellationToken) is IInvocationOperation
                    {
                        TargetMethod: { Name: "Release", Parameters.Length: 0 } target,
                        Instance: IFieldReferenceOperation { Instance: IInstanceReferenceOperation } field
                    } &&
                    MeridianAnalyzerSemanticHelpers.IsTypeOrDerivedFrom(
                        target.ContainingType, "System.Threading", "SemaphoreSlim") &&
                    SymbolEqualityComparer.Default.Equals(field.Field, semaphore))
                    return true;
            }
        }

        return false;
    }

    private static bool ReleasesCapturedSemaphoreContainer(
        SyntaxNodeAnalysisContext context, IObjectCreationOperation creation,
        INamedTypeSymbol type, ExpressionSyntax waitReceiver)
    {
        if (context.SemanticModel.GetOperation(waitReceiver, context.CancellationToken) is not
            IPropertyReferenceOperation { Instance.Syntax: ExpressionSyntax container } waited)
            return false;

        var captured = creation.Arguments.FirstOrDefault(argument =>
            argument.Value.Syntax is ExpressionSyntax expression &&
            MeridianAnalyzerSemanticHelpers.IsSameReference(
                container, expression, context.SemanticModel, context.CancellationToken))?.Parameter;
        if (captured is null) return false;

        foreach (var disposal in MeridianResourceOwnershipHelpers.GetDisposalMethods(type, context.CancellationToken))
        {
            var model = context.SemanticModel;
            if (disposal.SyntaxTree != model.SyntaxTree) continue;
            if (disposal.Body is not { Statements.Count: 2 } body ||
                body.Statements[0] is not IfStatementSyntax { Else: null } guard ||
                guard.Condition is not BinaryExpressionSyntax { RawKind: (int)SyntaxKind.EqualsExpression } comparison ||
                model.GetConstantValue(comparison.Right, context.CancellationToken) is not { HasValue: true, Value: 0 } ||
                model.GetOperation(comparison.Left, context.CancellationToken) is not IInvocationOperation
                {
                    TargetMethod: { Name: "Exchange", ContainingType: { } interlocked },
                    Arguments.Length: 2
                } exchange || interlocked.ToDisplayString() != "System.Threading.Interlocked" ||
                exchange.Arguments[0].Value is not IFieldReferenceOperation { Instance: IInstanceReferenceOperation } ||
                exchange.Arguments[1].Value.ConstantValue is not { HasValue: true, Value: 1 } ||
                guard.Statement is not ExpressionStatementSyntax { Expression: InvocationExpressionSyntax call } ||
                model.GetOperation(call, context.CancellationToken) is not IInvocationOperation releaseCall)
                continue;

            var forwarded = releaseCall.Arguments.FirstOrDefault(argument =>
                argument.Value is IParameterReferenceOperation parameter &&
                SymbolEqualityComparer.Default.Equals(parameter.Parameter, captured))?.Parameter;
            if (forwarded is null) continue;

            foreach (var reference in releaseCall.TargetMethod.DeclaringSyntaxReferences)
            {
                if (reference.GetSyntax(context.CancellationToken) is not MethodDeclarationSyntax
                    { Body: { Statements.Count: > 0 } releaseBody } release ||
                    releaseBody.Statements[0] is not ExpressionStatementSyntax
                    { Expression: InvocationExpressionSyntax invocation })
                    continue;

                var releaseModel = context.SemanticModel;
                if (release.SyntaxTree != releaseModel.SyntaxTree) continue;
                if (releaseModel.GetOperation(invocation, context.CancellationToken) is IInvocationOperation
                    {
                        TargetMethod: { Name: "Release", Parameters.Length: 0 } target,
                        Instance: IPropertyReferenceOperation
                        { Instance: IParameterReferenceOperation parameter } property
                    } &&
                    MeridianAnalyzerSemanticHelpers.IsTypeOrDerivedFrom(
                        target.ContainingType, "System.Threading", "SemaphoreSlim") &&
                    SymbolEqualityComparer.Default.Equals(property.Property, waited.Property) &&
                    SymbolEqualityComparer.Default.Equals(parameter.Parameter, forwarded))
                    return true;
            }
        }

        return false;
    }
}
