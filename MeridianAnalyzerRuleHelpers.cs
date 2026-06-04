using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Meridian.Analyzers;

internal static class MeridianAnalyzerRuleHelpers
{
    private static readonly string[] TestPathSegments =
    {
        "/tests/",
        ".Tests/",
        "/Test",
        "\\tests\\"
    };

    internal static bool IsTestPath(string filePath)
    {
        return MeridianAnalyzerSyntaxHelpers.PathContainsAny(filePath, TestPathSegments);
    }

    internal static bool IsControllerAction(MethodDeclarationSyntax methodDeclaration)
    {
        return methodDeclaration.Parent is ClassDeclarationSyntax classDeclaration &&
               MeridianAnalyzerSyntaxHelpers.IsControllerClass(classDeclaration) &&
               MeridianAnalyzerSyntaxHelpers.HasHttpMethodAttribute(methodDeclaration);
    }

    internal static MethodDeclarationSyntax? GetContainingMethod(SyntaxNode node)
    {
        return node.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();
    }

    internal static ClassDeclarationSyntax? GetContainingClass(SyntaxNode node)
    {
        return node.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().FirstOrDefault();
    }

    internal static bool HasModifier(SyntaxTokenList modifiers, SyntaxKind kind)
    {
        return modifiers.Any(modifier => modifier.IsKind(kind));
    }

    internal static bool IsAsyncLike(MethodDeclarationSyntax methodDeclaration)
    {
        var returnType = methodDeclaration.ReturnType.ToString();
        return HasModifier(methodDeclaration.Modifiers, SyntaxKind.AsyncKeyword) ||
               returnType.Contains("Task", StringComparison.Ordinal) ||
               returnType.Contains("ValueTask", StringComparison.Ordinal);
    }

    internal static bool HasCancellationTokenParameter(MethodDeclarationSyntax methodDeclaration)
    {
        return methodDeclaration.ParameterList.Parameters.Any(parameter =>
            parameter.Type?.ToString().EndsWith("CancellationToken", StringComparison.Ordinal) == true);
    }

    internal static string GetSimpleInvocationName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            IdentifierNameSyntax identifierName => identifierName.Identifier.ValueText,
            GenericNameSyntax genericName => genericName.Identifier.ValueText,
            _ => invocation.Expression.ToString()
        };
    }

    internal static bool IsMemberAccessNamed(ExpressionSyntax expression, string typeName, params string[] memberNames)
    {
        return expression is MemberAccessExpressionSyntax memberAccess &&
               string.Equals(memberAccess.Expression.ToString(), typeName, StringComparison.Ordinal) &&
               memberNames.Any(memberName => string.Equals(memberAccess.Name.Identifier.ValueText, memberName, StringComparison.Ordinal));
    }
}
