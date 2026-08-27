using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Meridian.Analyzer.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MER0062NameRecordConstructionArgumentsCodeFixProvider))]
[Shared]
public sealed class MER0062NameRecordConstructionArgumentsCodeFixProvider : CodeFixProvider
{
    private const string DiagnosticId = "MER0062";
    private const string Title = "Use named arguments";

    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(DiagnosticId);

    public override FixAllProvider GetFixAllProvider() => MER0062FixAllProvider.Instance;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics.FirstOrDefault();
        if (diagnostic is null)
            return;

        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root?.FindNode(diagnostic.Location.SourceSpan)
                .FirstAncestorOrSelf<BaseObjectCreationExpressionSyntax>() is not { } objectCreation)
            return;

        var semanticModel = await context.Document
            .GetSemanticModelAsync(context.CancellationToken)
            .ConfigureAwait(false);
        if (semanticModel is null || objectCreation.ArgumentList is null)
            return;

        if (!TryGetParameterNames(
                semanticModel,
                objectCreation,
                context.CancellationToken,
                out var parameterNamesByStart))
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                Title,
                cancellationToken => FixAsync(
                    context.Document,
                    objectCreation,
                    parameterNamesByStart,
                    cancellationToken),
                equivalenceKey: Title),
            diagnostic);
    }

    private static bool TryGetParameterNames(
        SemanticModel semanticModel,
        BaseObjectCreationExpressionSyntax objectCreation,
        CancellationToken cancellationToken,
        out ImmutableDictionary<int, string> parameterNamesByStart)
    {
        parameterNamesByStart = ImmutableDictionary<int, string>.Empty;
        if (semanticModel.GetSymbolInfo(objectCreation, cancellationToken).Symbol is not IMethodSymbol constructor)
            return false;

        var namedParameterOrdinals = new HashSet<int>();
        foreach (var argument in objectCreation.ArgumentList!.Arguments)
        {
            if (argument.NameColon is null)
                continue;

            var parameterOrdinal = FindParameterOrdinal(
                constructor.Parameters,
                argument.NameColon.Name.Identifier.ValueText);
            if (parameterOrdinal < 0)
                return false;

            namedParameterOrdinals.Add(parameterOrdinal);
        }

        var names = ImmutableDictionary.CreateBuilder<int, string>();
        var nextParameterOrdinal = 0;
        foreach (var argument in objectCreation.ArgumentList!.Arguments)
        {
            if (argument.NameColon is not null)
                continue;

            var parameterOrdinal = FindNextParameterOrdinal(
                constructor.Parameters,
                namedParameterOrdinals,
                nextParameterOrdinal);
            if (parameterOrdinal < 0 || parameterOrdinal >= constructor.Parameters.Length)
                return false;

            var parameter = constructor.Parameters[parameterOrdinal];
            names[argument.SpanStart] = parameter.Name;
            nextParameterOrdinal = parameter.IsParams
                ? parameterOrdinal
                : parameterOrdinal + 1;
        }

        parameterNamesByStart = names.ToImmutable();
        return true;
    }

    private static int FindParameterOrdinal(
        ImmutableArray<IParameterSymbol> parameters,
        string parameterName)
    {
        for (var index = 0; index < parameters.Length; index++)
        {
            if (parameters[index].Name == parameterName)
                return index;
        }

        return -1;
    }

    private static int FindNextParameterOrdinal(
        ImmutableArray<IParameterSymbol> parameters,
        HashSet<int> namedParameterOrdinals,
        int start)
    {
        for (var index = start; index < parameters.Length; index++)
        {
            if (!namedParameterOrdinals.Contains(index))
                return index;
        }

        return parameters.Length > 0 && parameters[parameters.Length - 1].IsParams
            ? parameters.Length - 1
            : -1;
    }

    private static async Task<Document> FixAsync(
        Document document,
        BaseObjectCreationExpressionSyntax objectCreation,
        ImmutableDictionary<int, string> parameterNamesByStart,
        CancellationToken cancellationToken)
    {
        var positionalArguments = objectCreation.ArgumentList!.Arguments
            .Where(argument => argument.NameColon is null)
            .ToImmutableArray();

        var changedObjectCreation = objectCreation.ReplaceNodes(
            positionalArguments,
            (original, _) => AddParameterName(
                original,
                parameterNamesByStart[original.SpanStart]));

        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        return document.WithSyntaxRoot(
            root!.ReplaceNode(objectCreation, changedObjectCreation));
    }

    private sealed class MER0062FixAllProvider : FixAllProvider
    {
        public static readonly MER0062FixAllProvider Instance = new();

        public override async Task<CodeAction?> GetFixAsync(FixAllContext fixAllContext)
        {
            IEnumerable<Document> documents = fixAllContext.Scope switch {
                FixAllScope.Document when fixAllContext.Document is { } document => new[] { document },
                FixAllScope.Project when fixAllContext.Project is { } project => project.Documents,
                FixAllScope.Solution => fixAllContext.Solution.Projects.SelectMany(project => project.Documents),
                _ => Array.Empty<Document>()
            };

            var changedSolution = fixAllContext.Solution;
            foreach (var document in documents)
            {
                fixAllContext.CancellationToken.ThrowIfCancellationRequested();
                var diagnostics = await fixAllContext
                    .GetDocumentDiagnosticsAsync(document)
                    .ConfigureAwait(false);
                if (diagnostics.IsDefaultOrEmpty)
                    continue;

                var currentDocument = changedSolution.GetDocument(document.Id)!;
                currentDocument = await FixDocumentAsync(
                        currentDocument,
                        diagnostics,
                        fixAllContext.CancellationToken)
                    .ConfigureAwait(false);
                var root = await currentDocument
                    .GetSyntaxRootAsync(fixAllContext.CancellationToken)
                    .ConfigureAwait(false);
                changedSolution = changedSolution.WithDocumentSyntaxRoot(document.Id, root!);
            }

            return CodeAction.Create(
                Title,
                _ => Task.FromResult(changedSolution),
                equivalenceKey: Title);
        }
    }

    private static async Task<Document> FixDocumentAsync(
        Document document,
        ImmutableArray<Diagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var currentDocument = document;
        foreach (var diagnostic in diagnostics.OrderByDescending(item => item.Location.SourceSpan.Start))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var root = await currentDocument.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var objectCreation = root?
                .DescendantNodes()
                .OfType<BaseObjectCreationExpressionSyntax>()
                .FirstOrDefault(node => node.SpanStart == diagnostic.Location.SourceSpan.Start);
            if (objectCreation is null || objectCreation.ArgumentList is null)
                continue;

            var semanticModel = await currentDocument
                .GetSemanticModelAsync(cancellationToken)
                .ConfigureAwait(false);
            if (semanticModel is null || !TryGetParameterNames(
                    semanticModel,
                    objectCreation,
                    cancellationToken,
                    out var parameterNamesByStart))
                continue;

            var positionalArguments = objectCreation.ArgumentList.Arguments
                .Where(argument => argument.NameColon is null)
                .ToImmutableArray();
            var changedObjectCreation = objectCreation.ReplaceNodes(
                positionalArguments,
                (original, _) => AddParameterName(
                    original,
                    parameterNamesByStart[original.SpanStart]));
            currentDocument = currentDocument.WithSyntaxRoot(
                root!.ReplaceNode(objectCreation, changedObjectCreation));
        }

        return currentDocument;
    }

    private static ArgumentSyntax AddParameterName(ArgumentSyntax argument, string parameterName)
    {
        var leadingTrivia = argument.GetLeadingTrivia();
        return argument
            .WithLeadingTrivia(SyntaxTriviaList.Empty)
            .WithNameColon(
                SyntaxFactory.NameColon(
                        SyntaxFactory.IdentifierName(parameterName))
                    .WithLeadingTrivia(leadingTrivia));
    }
}
