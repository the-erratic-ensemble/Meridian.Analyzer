using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Meridian.Analyzer;

internal static class MeridianResourceOwnershipHelpers
{
    internal static IEnumerable<IFieldSymbol> GetStoredFields(
        IParameterSymbol parameter, SemanticModel model, CancellationToken cancellationToken)
    {
        foreach (var reference in parameter.ContainingSymbol.DeclaringSyntaxReferences)
        {
            var declaration = reference.GetSyntax(cancellationToken);
            if (declaration.SyntaxTree != model.SyntaxTree) continue;
            // Primary constructors store parameters through instance field initializers.
            if (declaration is TypeDeclarationSyntax type)
            {
                foreach (var variable in type.Members.OfType<FieldDeclarationSyntax>()
                             .SelectMany(field => field.Declaration.Variables))
                {
                    if (variable.Initializer is { } initializer &&
                        SymbolEqualityComparer.Default.Equals(
                            model.GetSymbolInfo(initializer.Value, cancellationToken).Symbol, parameter) &&
                        model.GetDeclaredSymbol(variable, cancellationToken) is IFieldSymbol { IsStatic: false } field)
                        yield return field;
                }
            }
            else if (declaration is ConstructorDeclarationSyntax { Body: { } body })
            {
                foreach (var assignment in body.Statements.OfType<ExpressionStatementSyntax>()
                             .Select(statement => statement.Expression).OfType<AssignmentExpressionSyntax>())
                {
                    if (SymbolEqualityComparer.Default.Equals(
                            model.GetSymbolInfo(assignment.Right, cancellationToken).Symbol, parameter) &&
                        model.GetOperation(assignment.Left, cancellationToken) is
                            IFieldReferenceOperation { Instance: IInstanceReferenceOperation, Field: { IsStatic: false } field })
                        yield return field;
                }
            }
        }
    }

    internal static IEnumerable<MethodDeclarationSyntax> GetDisposalMethods(
        INamedTypeSymbol type, CancellationToken cancellationToken)
    {
        foreach (var contract in type.AllInterfaces.Where(contract =>
                     contract.ContainingNamespace.ToDisplayString() == "System" &&
                     contract.Name is "IDisposable" or "IAsyncDisposable"))
        {
            foreach (var member in contract.GetMembers().OfType<IMethodSymbol>())
            {
                if (type.FindImplementationForInterfaceMember(member) is not IMethodSymbol implementation)
                    continue;

                foreach (var reference in implementation.DeclaringSyntaxReferences)
                {
                    if (reference.GetSyntax(cancellationToken) is MethodDeclarationSyntax method)
                        yield return method;
                }
            }
        }
    }

    internal static bool IsAtomicTake(
        ExpressionSyntax expression, IFieldSymbol field, SemanticModel model, CancellationToken cancellationToken)
    {
        return model.GetOperation(expression, cancellationToken) is IInvocationOperation invocation &&
               invocation.TargetMethod is { Name: "Exchange", IsStatic: true } method &&
               method.ContainingType.ToDisplayString() == "System.Threading.Interlocked" &&
               invocation.Arguments.Length == 2 &&
               invocation.Arguments[0].Value is IFieldReferenceOperation
                   { Instance: IInstanceReferenceOperation } fieldReference &&
               SymbolEqualityComparer.Default.Equals(fieldReference.Field, field) &&
               invocation.Arguments[1].Value.ConstantValue is { HasValue: true, Value: null };
    }
}
