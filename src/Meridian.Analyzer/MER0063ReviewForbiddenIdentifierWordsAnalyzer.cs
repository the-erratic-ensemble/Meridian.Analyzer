using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0063ReviewForbiddenIdentifierWordsAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0063";

    private const string ForbiddenWordsOption = "meridian_forbidden_identifier_words";
    private const string AllowedNamesOption = "meridian_forbidden_identifier_allowed_names";

    private static readonly LocalizableString Title = "Review forbidden identifier word";

    private static readonly LocalizableString MessageFormat =
        "Identifier '{0}' uses configured word '{1}'";

    private static readonly LocalizableString Description =
        "Configured vocabulary in locally authored declaration names requires domain and structure review.";

    internal static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        MeridianDiagnosticCategories.Readability,
        DiagnosticSeverity.Warning,
        true,
        Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSemanticModelAction(AnalyzeSemanticModel);
    }

    private static void AnalyzeSemanticModel(SemanticModelAnalysisContext context)
    {
        var tree = context.SemanticModel.SyntaxTree;
        if (MeridianAnalyzerRuleHelpers.IsTestPath(tree.FilePath)) return;

        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(tree);
        var forbiddenWords = ReadConfiguredValues(options, ForbiddenWordsOption, StringComparer.OrdinalIgnoreCase);
        if (forbiddenWords.Length == 0) return;

        var allowedNames = ReadConfiguredValues(options, AllowedNamesOption, StringComparer.Ordinal);
        var root = tree.GetRoot(context.CancellationToken);

        foreach (var node in root.DescendantNodesAndSelf())
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            switch (node)
            {
                case BaseNamespaceDeclarationSyntax namespaceDeclaration:
                    AnalyzeNamespace(context, namespaceDeclaration, forbiddenWords, allowedNames);
                    break;
                case BaseTypeDeclarationSyntax typeDeclaration:
                    AnalyzeIdentifier(context, typeDeclaration, typeDeclaration.Identifier, forbiddenWords, allowedNames);
                    break;
                case DelegateDeclarationSyntax delegateDeclaration:
                    AnalyzeIdentifier(context, delegateDeclaration, delegateDeclaration.Identifier, forbiddenWords, allowedNames);
                    break;
                case MethodDeclarationSyntax methodDeclaration:
                    AnalyzeIdentifier(context, methodDeclaration, methodDeclaration.Identifier, forbiddenWords, allowedNames);
                    break;
                case LocalFunctionStatementSyntax localFunction:
                    AnalyzeIdentifier(context, localFunction, localFunction.Identifier, forbiddenWords, allowedNames);
                    break;
                case PropertyDeclarationSyntax propertyDeclaration:
                    AnalyzeIdentifier(context, propertyDeclaration, propertyDeclaration.Identifier, forbiddenWords, allowedNames);
                    break;
                case EventDeclarationSyntax eventDeclaration:
                    AnalyzeIdentifier(context, eventDeclaration, eventDeclaration.Identifier, forbiddenWords, allowedNames);
                    break;
                case EnumMemberDeclarationSyntax enumMember:
                    AnalyzeIdentifier(context, enumMember, enumMember.Identifier, forbiddenWords, allowedNames);
                    break;
                case VariableDeclaratorSyntax variable:
                    AnalyzeIdentifier(context, variable, variable.Identifier, forbiddenWords, allowedNames);
                    break;
                case ParameterSyntax parameter:
                    AnalyzeIdentifier(context, parameter, parameter.Identifier, forbiddenWords, allowedNames);
                    break;
                case TypeParameterSyntax typeParameter:
                    AnalyzeIdentifier(context, typeParameter, typeParameter.Identifier, forbiddenWords, allowedNames);
                    break;
                case ForEachStatementSyntax forEachStatement:
                    AnalyzeIdentifier(context, forEachStatement, forEachStatement.Identifier, forbiddenWords, allowedNames);
                    break;
                case CatchDeclarationSyntax catchDeclaration:
                    AnalyzeIdentifier(context, catchDeclaration, catchDeclaration.Identifier, forbiddenWords, allowedNames);
                    break;
                case SingleVariableDesignationSyntax designation:
                    AnalyzeIdentifier(context, designation, designation.Identifier, forbiddenWords, allowedNames);
                    break;
                case FromClauseSyntax fromClause:
                    AnalyzeIdentifier(context, fromClause, fromClause.Identifier, forbiddenWords, allowedNames);
                    break;
                case LetClauseSyntax letClause:
                    AnalyzeIdentifier(context, letClause, letClause.Identifier, forbiddenWords, allowedNames);
                    break;
                case JoinClauseSyntax joinClause:
                    AnalyzeIdentifier(context, joinClause, joinClause.Identifier, forbiddenWords, allowedNames);
                    break;
                case JoinIntoClauseSyntax joinIntoClause:
                    AnalyzeIdentifier(context, joinIntoClause, joinIntoClause.Identifier, forbiddenWords, allowedNames);
                    break;
                case QueryContinuationSyntax continuation:
                    AnalyzeIdentifier(context, continuation, continuation.Identifier, forbiddenWords, allowedNames);
                    break;
                case TupleElementSyntax tupleElement:
                    AnalyzeIdentifier(context, tupleElement, tupleElement.Identifier, forbiddenWords, allowedNames);
                    break;
                case TupleExpressionSyntax tupleExpression:
                    AnalyzeTupleExpression(context, tupleExpression, forbiddenWords, allowedNames);
                    break;
            }
        }
    }

    private static void AnalyzeNamespace(
        SemanticModelAnalysisContext context,
        BaseNamespaceDeclarationSyntax declaration,
        string[] forbiddenWords,
        string[] allowedNames)
    {
        foreach (var identifier in declaration.Name.DescendantTokens().Where(token => token.RawKind != 0))
        {
            if (!identifier.IsKind(SyntaxKind.IdentifierToken)) continue;
            if (AnalyzeIdentifier(context, declaration, identifier, forbiddenWords, allowedNames)) return;
        }
    }

    private static void AnalyzeTupleExpression(
        SemanticModelAnalysisContext context,
        TupleExpressionSyntax expression,
        string[] forbiddenWords,
        string[] allowedNames)
    {
        foreach (var argument in expression.Arguments)
        {
            if (argument.NameColon is { } nameColon)
                AnalyzeIdentifier(
                    context,
                    nameColon,
                    nameColon.Name.Identifier,
                    forbiddenWords,
                    allowedNames,
                    inspectOwnership: false);
        }
    }

    private static bool AnalyzeIdentifier(
        SemanticModelAnalysisContext context,
        SyntaxNode declaration,
        SyntaxToken identifier,
        string[] forbiddenWords,
        string[] allowedNames,
        bool inspectOwnership = true)
    {
        if (identifier.RawKind == 0 || identifier.IsMissing) return false;

        var name = identifier.ValueText;
        if (allowedNames.Contains(name, StringComparer.Ordinal)) return false;

        var forbiddenWord = FindForbiddenWord(name, forbiddenWords);
        if (forbiddenWord is null) return false;

        if (inspectOwnership)
        {
            var symbol = context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken);
            if (symbol is not null &&
                (HasContractOwner(symbol) || !IsPrimaryDeclaration(symbol, declaration)))
                return false;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, identifier.GetLocation(), name, forbiddenWord));
        return true;
    }

    private static string? FindForbiddenWord(string name, string[] forbiddenWords)
    {
        string? match = null;
        var matchIndex = int.MaxValue;

        foreach (var forbiddenWord in forbiddenWords)
        {
            var index = name.IndexOf(forbiddenWord, StringComparison.OrdinalIgnoreCase);
            if (index < 0 || index > matchIndex ||
                index == matchIndex && match is not null && forbiddenWord.Length <= match.Length)
                continue;

            match = forbiddenWord;
            matchIndex = index;
        }

        return match;
    }

    private static string[] ReadConfiguredValues(
        AnalyzerConfigOptions options,
        string optionName,
        StringComparer comparer)
    {
        if (!options.TryGetValue(optionName, out var configured)) return Array.Empty<string>();

        return configured
            .Split('|')
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .Distinct(comparer)
            .ToArray();
    }

    private static bool HasContractOwner(ISymbol symbol)
    {
        var owner = symbol switch
        {
            IParameterSymbol parameter => parameter.ContainingSymbol,
            _ => symbol
        };

        if (owner is IMethodSymbol { OverriddenMethod: not null } ||
            owner is IPropertySymbol { OverriddenProperty: not null } ||
            owner is IEventSymbol { OverriddenEvent: not null })
            return true;

        if (owner is IMethodSymbol { ExplicitInterfaceImplementations.Length: > 0 } ||
            owner is IPropertySymbol { ExplicitInterfaceImplementations.Length: > 0 } ||
            owner is IEventSymbol { ExplicitInterfaceImplementations.Length: > 0 })
            return true;

        var containingType = owner.ContainingType;
        if (containingType is null || containingType.TypeKind == TypeKind.Interface) return false;

        foreach (var interfaceType in containingType.AllInterfaces)
        {
            foreach (var interfaceMember in interfaceType.GetMembers(owner.Name))
            {
                if (SymbolEqualityComparer.Default.Equals(
                        containingType.FindImplementationForInterfaceMember(interfaceMember),
                        owner))
                    return true;
            }
        }

        return false;
    }

    private static bool IsPrimaryDeclaration(ISymbol symbol, SyntaxNode declaration)
    {
        if (symbol is IMethodSymbol { PartialDefinitionPart: { } definition }) symbol = definition;

        var references = symbol.DeclaringSyntaxReferences;
        if (references.Length < 2) return true;

        var first = references
            .OrderBy(reference => reference.SyntaxTree.FilePath, StringComparer.Ordinal)
            .ThenBy(reference => reference.Span.Start)
            .First();

        return first.SyntaxTree == declaration.SyntaxTree && first.Span == declaration.Span;
    }
}
