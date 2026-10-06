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
    private const string ForbiddenSuffixesOption = "meridian_forbidden_identifier_suffixes";
    private const string AllowedNamesOption = "meridian_forbidden_identifier_allowed_names";
    private const string AllowedFragmentsOption = "meridian_forbidden_identifier_allowed_fragments";

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
        var forbiddenWords = new ForbiddenVocabulary(
            ReadConfiguredValues(options, ForbiddenWordsOption, StringComparer.OrdinalIgnoreCase),
            ReadConfiguredValues(options, ForbiddenSuffixesOption, StringComparer.OrdinalIgnoreCase));
        if (forbiddenWords.IsEmpty) return;

        var allowedNames = ReadConfiguredValues(options, AllowedNamesOption, StringComparer.Ordinal);
        var allowedFragments = ReadConfiguredValues(
            options,
            AllowedFragmentsOption,
            StringComparer.OrdinalIgnoreCase);
        var root = tree.GetRoot(context.CancellationToken);

        foreach (var node in root.DescendantNodesAndSelf())
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            switch (node)
            {
                case BaseNamespaceDeclarationSyntax namespaceDeclaration:
                    AnalyzeNamespace(
                        context,
                        namespaceDeclaration,
                        forbiddenWords,
                        allowedNames,
                        allowedFragments);
                    break;
                case BaseTypeDeclarationSyntax typeDeclaration:
                    AnalyzeIdentifier(
                        context,
                        typeDeclaration,
                        typeDeclaration.Identifier,
                        forbiddenWords,
                        allowedNames,
                        allowedFragments);
                    break;
                case DelegateDeclarationSyntax delegateDeclaration:
                    AnalyzeIdentifier(
                        context,
                        delegateDeclaration,
                        delegateDeclaration.Identifier,
                        forbiddenWords,
                        allowedNames,
                        allowedFragments);
                    break;
                case MethodDeclarationSyntax methodDeclaration:
                    AnalyzeIdentifier(
                        context,
                        methodDeclaration,
                        methodDeclaration.Identifier,
                        forbiddenWords,
                        allowedNames,
                        allowedFragments);
                    break;
                case LocalFunctionStatementSyntax localFunction:
                    AnalyzeIdentifier(
                        context,
                        localFunction,
                        localFunction.Identifier,
                        forbiddenWords,
                        allowedNames,
                        allowedFragments);
                    break;
                case PropertyDeclarationSyntax propertyDeclaration:
                    AnalyzeIdentifier(
                        context,
                        propertyDeclaration,
                        propertyDeclaration.Identifier,
                        forbiddenWords,
                        allowedNames,
                        allowedFragments);
                    break;
                case EventDeclarationSyntax eventDeclaration:
                    AnalyzeIdentifier(
                        context,
                        eventDeclaration,
                        eventDeclaration.Identifier,
                        forbiddenWords,
                        allowedNames,
                        allowedFragments);
                    break;
                case EnumMemberDeclarationSyntax enumMember:
                    AnalyzeIdentifier(
                        context,
                        enumMember,
                        enumMember.Identifier,
                        forbiddenWords,
                        allowedNames,
                        allowedFragments);
                    break;
                case VariableDeclaratorSyntax variable:
                    AnalyzeIdentifier(
                        context,
                        variable,
                        variable.Identifier,
                        forbiddenWords,
                        allowedNames,
                        allowedFragments);
                    break;
                case ParameterSyntax parameter:
                    AnalyzeIdentifier(
                        context,
                        parameter,
                        parameter.Identifier,
                        forbiddenWords,
                        allowedNames,
                        allowedFragments);
                    break;
                case TypeParameterSyntax typeParameter:
                    AnalyzeIdentifier(
                        context,
                        typeParameter,
                        typeParameter.Identifier,
                        forbiddenWords,
                        allowedNames,
                        allowedFragments);
                    break;
                case ForEachStatementSyntax forEachStatement:
                    AnalyzeIdentifier(
                        context,
                        forEachStatement,
                        forEachStatement.Identifier,
                        forbiddenWords,
                        allowedNames,
                        allowedFragments);
                    break;
                case CatchDeclarationSyntax catchDeclaration:
                    AnalyzeIdentifier(
                        context,
                        catchDeclaration,
                        catchDeclaration.Identifier,
                        forbiddenWords,
                        allowedNames,
                        allowedFragments);
                    break;
                case SingleVariableDesignationSyntax designation:
                    AnalyzeIdentifier(
                        context,
                        designation,
                        designation.Identifier,
                        forbiddenWords,
                        allowedNames,
                        allowedFragments);
                    break;
                case FromClauseSyntax fromClause:
                    AnalyzeIdentifier(
                        context,
                        fromClause,
                        fromClause.Identifier,
                        forbiddenWords,
                        allowedNames,
                        allowedFragments);
                    break;
                case LetClauseSyntax letClause:
                    AnalyzeIdentifier(
                        context,
                        letClause,
                        letClause.Identifier,
                        forbiddenWords,
                        allowedNames,
                        allowedFragments);
                    break;
                case JoinClauseSyntax joinClause:
                    AnalyzeIdentifier(
                        context,
                        joinClause,
                        joinClause.Identifier,
                        forbiddenWords,
                        allowedNames,
                        allowedFragments);
                    break;
                case JoinIntoClauseSyntax joinIntoClause:
                    AnalyzeIdentifier(
                        context,
                        joinIntoClause,
                        joinIntoClause.Identifier,
                        forbiddenWords,
                        allowedNames,
                        allowedFragments);
                    break;
                case QueryContinuationSyntax continuation:
                    AnalyzeIdentifier(
                        context,
                        continuation,
                        continuation.Identifier,
                        forbiddenWords,
                        allowedNames,
                        allowedFragments);
                    break;
                case TupleElementSyntax tupleElement:
                    AnalyzeIdentifier(
                        context,
                        tupleElement,
                        tupleElement.Identifier,
                        forbiddenWords,
                        allowedNames,
                        allowedFragments);
                    break;
                case TupleExpressionSyntax tupleExpression:
                    AnalyzeTupleExpression(
                        context,
                        tupleExpression,
                        forbiddenWords,
                        allowedNames,
                        allowedFragments);
                    break;
            }
        }
    }

    private static void AnalyzeNamespace(
        SemanticModelAnalysisContext context,
        BaseNamespaceDeclarationSyntax declaration,
        ForbiddenVocabulary forbiddenWords,
        string[] allowedNames,
        string[] allowedFragments)
    {
        foreach (var identifier in declaration.Name.DescendantTokens().Where(token => token.RawKind != 0))
        {
            if (!identifier.IsKind(SyntaxKind.IdentifierToken)) continue;
            if (AnalyzeIdentifier(
                    context,
                    declaration,
                    identifier,
                    forbiddenWords,
                    allowedNames,
                    allowedFragments))
                return;
        }
    }

    private static void AnalyzeTupleExpression(
        SemanticModelAnalysisContext context,
        TupleExpressionSyntax expression,
        ForbiddenVocabulary forbiddenWords,
        string[] allowedNames,
        string[] allowedFragments)
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
                    allowedFragments,
                    inspectOwnership: false);
        }
    }

    private static bool AnalyzeIdentifier(
        SemanticModelAnalysisContext context,
        SyntaxNode declaration,
        SyntaxToken identifier,
        ForbiddenVocabulary forbiddenWords,
        string[] allowedNames,
        string[] allowedFragments,
        bool inspectOwnership = true)
    {
        if (identifier.RawKind == 0 || identifier.IsMissing) return false;

        var name = identifier.ValueText;
        if (allowedNames.Contains(name, StringComparer.Ordinal)) return false;

        var forbiddenWord = FindForbiddenWord(name, forbiddenWords, allowedFragments);
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

    private static string? FindForbiddenWord(
        string name,
        ForbiddenVocabulary forbiddenWords,
        string[] allowedFragments)
    {
        string? match = null;
        var matchIndex = int.MaxValue;

        foreach (var forbiddenWord in forbiddenWords.SubstringMatches)
        {
            var searchIndex = 0;
            while (searchIndex <= name.Length - forbiddenWord.Length)
            {
                var index = name.IndexOf(
                    forbiddenWord,
                    searchIndex,
                    StringComparison.OrdinalIgnoreCase);
                if (index < 0) break;

                if (!IsWithinAllowedFragment(name, index, forbiddenWord.Length, allowedFragments) &&
                    (index < matchIndex ||
                     index == matchIndex && (match is null || forbiddenWord.Length > match.Length)))
                {
                    match = forbiddenWord;
                    matchIndex = index;
                }

                searchIndex = index + 1;
            }
        }

        foreach (var forbiddenSuffix in forbiddenWords.SuffixMatches)
        {
            var index = name.Length - forbiddenSuffix.Length;
            if (index < 0 ||
                !name.EndsWith(forbiddenSuffix, StringComparison.OrdinalIgnoreCase) ||
                IsWithinAllowedFragment(name, index, forbiddenSuffix.Length, allowedFragments) ||
                index > matchIndex ||
                index == matchIndex && match is not null && forbiddenSuffix.Length <= match.Length)
                continue;

            match = forbiddenSuffix;
            matchIndex = index;
        }

        return match;
    }

    private static bool IsWithinAllowedFragment(
        string name,
        int forbiddenIndex,
        int forbiddenLength,
        string[] allowedFragments)
    {
        var forbiddenEnd = forbiddenIndex + forbiddenLength;

        foreach (var allowedFragment in allowedFragments)
        {
            var searchIndex = 0;
            while (searchIndex <= forbiddenIndex)
            {
                var fragmentIndex = name.IndexOf(
                    allowedFragment,
                    searchIndex,
                    StringComparison.OrdinalIgnoreCase);
                if (fragmentIndex < 0 || fragmentIndex > forbiddenIndex) break;
                if (fragmentIndex + allowedFragment.Length >= forbiddenEnd) return true;

                searchIndex = fragmentIndex + 1;
            }
        }

        return false;
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

    private sealed class ForbiddenVocabulary
    {
        public ForbiddenVocabulary(string[] substringMatches, string[] suffixMatches)
        {
            SubstringMatches = substringMatches;
            SuffixMatches = suffixMatches;
        }

        public string[] SubstringMatches { get; }

        public string[] SuffixMatches { get; }

        public bool IsEmpty => SubstringMatches.Length == 0 && SuffixMatches.Length == 0;
    }
}
