using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ExternalFalsePositiveSuppressor : DiagnosticSuppressor
{
    private static readonly SuppressionDescriptor LexicalDisposal = new(
        "MERS0001", "CA2000", "The configured factory's returned resource is directly owned by a using declaration.");
    private static readonly SuppressionDescriptor BorrowedField = new(
        "MERS0002", "CA2213", "The configured field borrows a constructor-supplied resource.");
    private static readonly SuppressionDescriptor CasingValidation = new(
        "MERS0003", "CA1862", "Comparison with the same value's invariant casing validates its existing case.");
    private static readonly SuppressionDescriptor ForwardedCallerArgument = new(
        "MERS0004", "S3236", "A wrapper explicitly forwards its parameter to a caller-argument-expression parameter.");
    private static readonly SuppressionDescriptor AbstractStaticProperty = new(
        "MERS0005", "S2743", "A static abstract interface property declares a contract and has no backing storage.");
    private static readonly SuppressionDescriptor ReturnedCarrier = new(
        "MERS0006", "CA2000", "The returned positional record transfers disposal through its configured ownership property.");

    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions =>
        ImmutableArray.Create(LexicalDisposal, BorrowedField, CasingValidation,
            ForwardedCallerArgument, AbstractStaticProperty, ReturnedCarrier);

    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        foreach (var diagnostic in context.ReportedDiagnostics)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (diagnostic.Location.SourceTree is not { } tree) continue;
            var model = context.GetSemanticModel(tree);
            var node = tree.GetRoot(context.CancellationToken).FindNode(
                diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
            var descriptor = diagnostic.Id switch
            {
                "CA2000" when IsLexicallyDisposed(context, model, node, diagnostic) => LexicalDisposal,
                "CA2000" when IsReturnedCarrier(context, model, node, diagnostic) => ReturnedCarrier,
                "CA2213" when IsBorrowedField(context, model, node) => BorrowedField,
                "CA1862" when IsCasingValidation(context, model, node) => CasingValidation,
                "S3236" when IsForwardedCallerArgument(context, model, node) => ForwardedCallerArgument,
                "S2743" when IsAbstractStaticProperty(context, model, node) => AbstractStaticProperty,
                _ => null
            };
            if (descriptor is not null)
                context.ReportSuppression(Suppression.Create(descriptor, diagnostic));
        }
    }

    private static bool IsConfigured(
        SuppressionAnalysisContext context, SyntaxTree tree, string option, ISymbol symbol)
    {
        var id = symbol.GetDocumentationCommentId();
        return id is not null &&
               context.Options.AnalyzerConfigOptionsProvider.GetOptions(tree).TryGetValue(option, out var configured) &&
               configured.Split('|').Any(value => string.Equals(value.Trim(), id, StringComparison.Ordinal));
    }

    private static bool IsLexicallyDisposed(
        SuppressionAnalysisContext context, SemanticModel model, SyntaxNode node, Diagnostic diagnostic)
    {
        var invocation = node.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().FirstOrDefault();
        return invocation is not null && invocation.Span == diagnostic.Location.SourceSpan &&
               invocation.Parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax variable } initializer &&
               initializer.Value == invocation &&
               variable.Parent?.Parent is LocalDeclarationStatementSyntax declaration &&
               declaration.UsingKeyword.IsKind(SyntaxKind.UsingKeyword) &&
               model.GetOperation(invocation, context.CancellationToken) is IInvocationOperation operation &&
               MeridianAnalyzerSemanticHelpers.Implements(operation.Type, "System", "IDisposable") &&
               IsConfigured(context, node.SyntaxTree, "meridian_disposable_factories",
                   (operation.TargetMethod.ReducedFrom ?? operation.TargetMethod).OriginalDefinition);
    }

    private static bool IsBorrowedField(
        SuppressionAnalysisContext context, SemanticModel model, SyntaxNode node)
    {
        var variable = node.AncestorsAndSelf().OfType<VariableDeclaratorSyntax>().FirstOrDefault();
        if (variable is null || model.GetDeclaredSymbol(variable, context.CancellationToken) is not
                IFieldSymbol { IsStatic: false } field ||
            !IsConfigured(context, node.SyntaxTree, "meridian_borrowed_resource_fields", field))
            return false;

        // A later assignment of a newly owned resource invalidates the borrowed-field contract.
        foreach (var reference in field.ContainingType.DeclaringSyntaxReferences)
        {
            var declaration = reference.GetSyntax(context.CancellationToken);
            var fieldModel = context.GetSemanticModel(declaration.SyntaxTree);
            foreach (var assignment in declaration.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (!SymbolEqualityComparer.Default.Equals(
                        fieldModel.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol, field))
                    continue;
                if (fieldModel.GetSymbolInfo(assignment.Right, context.CancellationToken).Symbol is not
                        IParameterSymbol { ContainingSymbol: IMethodSymbol { MethodKind: MethodKind.Constructor } } ||
                    assignment.Ancestors().OfType<ConstructorDeclarationSyntax>().FirstOrDefault() is null)
                    return false;
            }

            foreach (var argument in declaration.DescendantNodes().OfType<ArgumentSyntax>()
                         .Where(argument => !argument.RefKindKeyword.IsKind(SyntaxKind.None)))
            {
                if (SymbolEqualityComparer.Default.Equals(
                        fieldModel.GetSymbolInfo(argument.Expression, context.CancellationToken).Symbol, field) &&
                    (argument.Parent?.Parent is not InvocationExpressionSyntax invocation ||
                     !MeridianResourceOwnershipHelpers.IsAtomicTake(
                         invocation, field, fieldModel, context.CancellationToken)))
                    return false;
            }
        }

        return field.ContainingType.InstanceConstructors.SelectMany(constructor => constructor.Parameters)
            .Any(parameter => MeridianResourceOwnershipHelpers.GetStoredFields(parameter, model, context.CancellationToken)
                .Any(stored => SymbolEqualityComparer.Default.Equals(stored, field)));
    }

    private static bool IsReturnedCarrier(
        SuppressionAnalysisContext context, SemanticModel model, SyntaxNode node, Diagnostic diagnostic)
    {
        var allocation = node.AncestorsAndSelf().OfType<BaseObjectCreationExpressionSyntax>().FirstOrDefault();
        if (allocation is null || allocation.Span != diagnostic.Location.SourceSpan ||
            allocation.Parent is not ArgumentSyntax argument || argument.Expression != allocation ||
            model.GetOperation(argument, context.CancellationToken) is not IArgumentOperation
            { Parameter: { } parameter, Parent: IObjectCreationOperation { Constructor: { } constructor } carrier } ||
            carrier.Syntax.Parent is not ReturnStatementSyntax ||
            !MeridianAnalyzerSemanticHelpers.Implements(
                model.GetTypeInfo(allocation, context.CancellationToken).Type, "System", "IDisposable") ||
            !constructor.ContainingType.IsRecord)
            return false;

        var property = constructor.ContainingType.GetMembers(parameter.Name).OfType<IPropertySymbol>()
            .FirstOrDefault(candidate => SymbolEqualityComparer.Default.Equals(candidate.Type, parameter.Type));
        return property is not null &&
               property.DeclaringSyntaxReferences.Any(reference => reference.GetSyntax(context.CancellationToken) is ParameterSyntax) &&
               IsConfigured(context, node.SyntaxTree, "meridian_disposable_carrier_properties", property) &&
               constructor.DeclaringSyntaxReferences.Any(reference =>
                   reference.GetSyntax(context.CancellationToken) is RecordDeclarationSyntax { BaseList: null } record &&
                   record.Members.All(member => member is PropertyDeclarationSyntax { Initializer: null }));
    }

    private static bool IsCasingValidation(
        SuppressionAnalysisContext context, SemanticModel model, SyntaxNode node)
    {
        var comparison = node.AncestorsAndSelf().OfType<BinaryExpressionSyntax>().FirstOrDefault(binary =>
            binary.IsKind(SyntaxKind.EqualsExpression) || binary.IsKind(SyntaxKind.NotEqualsExpression));
        if (comparison is null) return false;
        var left = model.GetOperation(comparison.Left, context.CancellationToken);
        var right = model.GetOperation(comparison.Right, context.CancellationToken);
        return IsCaseOfSameValue(left, right) || IsCaseOfSameValue(right, left);
    }

    private static bool IsCaseOfSameValue(IOperation? value, IOperation? transformed)
    {
        return transformed is IInvocationOperation
               {
                   TargetMethod: { Name: "ToUpperInvariant" or "ToLowerInvariant", Parameters.Length: 0 } method,
                   Instance: { } receiver
               } && method.ContainingType.SpecialType == SpecialType.System_String &&
               SameStableValue(value, receiver);
    }

    private static bool SameStableValue(IOperation? left, IOperation? right)
    {
        return (left, right) switch
        {
            (IParameterReferenceOperation a, IParameterReferenceOperation b) =>
                SymbolEqualityComparer.Default.Equals(a.Parameter, b.Parameter),
            (ILocalReferenceOperation a, ILocalReferenceOperation b) =>
                SymbolEqualityComparer.Default.Equals(a.Local, b.Local),
            (IInstanceReferenceOperation a, IInstanceReferenceOperation b) =>
                a.ReferenceKind == b.ReferenceKind && SymbolEqualityComparer.Default.Equals(a.Type, b.Type),
            (IPropertyReferenceOperation a, IPropertyReferenceOperation b) =>
                a.Arguments.Length == 0 && b.Arguments.Length == 0 &&
                SymbolEqualityComparer.Default.Equals(a.Property, b.Property) &&
                SameStableValue(a.Instance, b.Instance) &&
                (a.Property.DeclaringSyntaxReferences.Any(reference => IsAutoProperty(reference.GetSyntax())) ||
                 a.Property.DeclaringSyntaxReferences.Length == 0 &&
                 a.Property.GetMethod?.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() ==
                     "System.Runtime.CompilerServices.CompilerGeneratedAttribute") == true),
            _ => false
        };
    }

    private static bool IsAutoProperty(SyntaxNode declaration)
    {
        return declaration is ParameterSyntax ||
               declaration is PropertyDeclarationSyntax { ExpressionBody: null, AccessorList: { } accessors } &&
               accessors.Accessors.All(accessor => accessor.Body is null && accessor.ExpressionBody is null);
    }

    private static bool IsForwardedCallerArgument(
        SuppressionAnalysisContext context, SemanticModel model, SyntaxNode node)
    {
        var argument = node.AncestorsAndSelf().OfType<ArgumentSyntax>().FirstOrDefault();
        return argument is not null && model.GetOperation(argument, context.CancellationToken) is IArgumentOperation
               { Value: IParameterReferenceOperation forwarded, Parameter: { } target } &&
               forwarded.Parameter.Type.SpecialType == SpecialType.System_String &&
               target.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() ==
                   "System.Runtime.CompilerServices.CallerArgumentExpressionAttribute");
    }

    private static bool IsAbstractStaticProperty(
        SuppressionAnalysisContext context, SemanticModel model, SyntaxNode node)
    {
        var declaration = node.AncestorsAndSelf().OfType<PropertyDeclarationSyntax>().FirstOrDefault();
        return declaration is not null && model.GetDeclaredSymbol(declaration, context.CancellationToken) is
            { IsStatic: true, IsAbstract: true, ContainingType.TypeKind: TypeKind.Interface };
    }
}
