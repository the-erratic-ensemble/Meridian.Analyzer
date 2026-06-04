using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Meridian.Analyzer;

internal static class MeridianAnalyzerSyntaxHelpers
{
    internal static bool HasAttribute(MemberDeclarationSyntax member, params string[] attributeNames)
    {
        return member.AttributeLists
            .SelectMany(attributeList => attributeList.Attributes)
            .Any(attribute => IsAttributeNamed(attribute, attributeNames));
    }

    internal static IEnumerable<AttributeSyntax> GetAttributes(MemberDeclarationSyntax member, params string[] attributeNames)
    {
        return member.AttributeLists
            .SelectMany(attributeList => attributeList.Attributes)
            .Where(attribute => IsAttributeNamed(attribute, attributeNames));
    }

    internal static bool IsAttributeNamed(AttributeSyntax attribute, params string[] attributeNames)
    {
        var attributeName = NormalizeAttributeName(GetNameText(attribute.Name));
        return attributeNames.Any(candidate => string.Equals(attributeName, NormalizeAttributeName(candidate), StringComparison.Ordinal));
    }

    internal static bool InheritsFrom(ClassDeclarationSyntax classDeclaration, params string[] typeNames)
    {
        return classDeclaration.BaseList?.Types.Any(baseType => {
            var typeName = GetTypeName(baseType.Type);
            return typeNames.Any(candidate => string.Equals(typeName, candidate, StringComparison.Ordinal));
        }) == true;
    }

    internal static bool IsControllerClass(ClassDeclarationSyntax classDeclaration)
    {
        return classDeclaration.Identifier.ValueText.EndsWith("Controller", StringComparison.Ordinal) ||
               InheritsFrom(classDeclaration, "ControllerBase", "BaseApiController", "BaseAdminController");
    }

    internal static bool HasHttpMethodAttribute(MethodDeclarationSyntax methodDeclaration)
    {
        return HasAttribute(
            methodDeclaration,
            "HttpGet",
            "HttpPost",
            "HttpPut",
            "HttpDelete",
            "HttpPatch",
            "HttpHead",
            "HttpOptions");
    }

    internal static string? GetFirstStringArgument(AttributeSyntax attribute)
    {
        return attribute.ArgumentList?.Arguments
            .Select(argument => GetStringLiteral(argument.Expression))
            .FirstOrDefault(value => value is not null);
    }

    internal static string? GetStringLiteral(ExpressionSyntax expression)
    {
        return expression is LiteralExpressionSyntax { Token.Value: string literal }
            ? literal
            : null;
    }

    internal static string GetSimpleName(SimpleNameSyntax name)
    {
        return name switch
        {
            GenericNameSyntax genericName => genericName.Identifier.ValueText,
            IdentifierNameSyntax identifierName => identifierName.Identifier.ValueText,
            _ => name.Identifier.ValueText
        };
    }

    internal static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }

    internal static bool PathContains(string path, string segment)
    {
        return NormalizePath(path).IndexOf(segment, StringComparison.Ordinal) >= 0;
    }

    internal static bool PathContainsAny(string path, params string[] segments)
    {
        return segments.Any(segment => PathContains(path, segment));
    }

    internal static bool StartsWithOrdinal(string value, string prefix)
    {
        return value.StartsWith(prefix, StringComparison.Ordinal);
    }

    private static string NormalizeAttributeName(string name)
    {
        return name.EndsWith("Attribute", StringComparison.Ordinal)
            ? name.Substring(0, name.Length - "Attribute".Length)
            : name;
    }

    private static string GetNameText(NameSyntax name)
    {
        return name switch
        {
            IdentifierNameSyntax identifierName => identifierName.Identifier.ValueText,
            GenericNameSyntax genericName => genericName.Identifier.ValueText,
            QualifiedNameSyntax qualifiedName => GetNameText(qualifiedName.Right),
            AliasQualifiedNameSyntax aliasQualifiedName => aliasQualifiedName.Name.Identifier.ValueText,
            _ => name.ToString()
        };
    }

    private static string GetTypeName(TypeSyntax type)
    {
        return type switch
        {
            IdentifierNameSyntax identifierName => identifierName.Identifier.ValueText,
            GenericNameSyntax genericName => genericName.Identifier.ValueText,
            QualifiedNameSyntax qualifiedName => GetNameText(qualifiedName.Right),
            SimpleNameSyntax simpleName => simpleName.Identifier.ValueText,
            _ => type.ToString()
        };
    }
}
