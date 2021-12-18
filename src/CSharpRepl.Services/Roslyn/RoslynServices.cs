// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;

namespace CSharpRepl.Services.Roslyn;

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Completion;

using Disassembly;

using Logging;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Caching.Memory;

using PrettyPrompt.Consoles;
using PrettyPrompt.Highlighting;

using References;

using Scripting;

using SymbolExploration;

using SyntaxHighlighting;

/// <summary>
///     The main entry point of all services. This is a facade for other services that manages their
///     startup and
///     initialization.
///     It also ensures two different areas of the Roslyn API, the Scripting and Workspace APIs, remain
///     in sync.
/// </summary>
public sealed class RoslynServices
{
    // when this Initialization task successfully completes, all the above members will not be null.
    [ MemberNotNull(
        nameof(RoslynServices._scriptRunner),
        nameof(RoslynServices._workspaceManager),
        nameof(RoslynServices._disassembler),
        nameof(RoslynServices._prettyPrinter),
        nameof(RoslynServices._symbolExplorer),
        nameof(RoslynServices._autocompleteService),
        nameof(RoslynServices._referenceService),
        nameof(RoslynServices._compilationOptions)
    ) ]
    private Task Initialization { get; }

    public RoslynServices(IConsole console, Configuration config, ITraceLogger logger)
    {
        MemoryCache cache = new(new MemoryCacheOptions());
        _logger = logger;
        _highlighter = new SyntaxHighlighter(cache, config.Theme);
        // initialization of roslyn and all dependent services is slow! do it asynchronously so we don't increase startup time.
        Initialization = Task.Run(
            () =>
            {
                logger.Log("Starting background initialization");
                _referenceService = new AssemblyReferenceService(config, logger);

                _compilationOptions = new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    usings: _referenceService.Usings.Select(u => u.Name.ToString()),
                    allowUnsafe: true
                );

                // the script runner is used to actually execute the scripts, and the workspace manager
                // is updated alongside. The workspace is a datamodel used in "editor services" like
                // syntax highlighting, autocompletion, and roslyn symbol queries.
                _scriptRunner = new ScriptRunner(_compilationOptions, _referenceService, console);
                _workspaceManager = new WorkspaceManager(
                    _compilationOptions,
                    _referenceService,
                    logger
                );

                _disassembler = new Disassembler(
                    _compilationOptions,
                    _referenceService,
                    _scriptRunner
                );
                _prettyPrinter = new PrettyPrinter();
                _symbolExplorer = new SymbolExplorer(_referenceService, _scriptRunner);
                _autocompleteService = new AutoCompleteService(cache);
                logger.Log("Background initialization complete");
            }
        );
        Initialization.ContinueWith(
            task => console.WriteErrorLine(task.Exception?.Message ?? "Unknown error"),
            TaskContinuationOptions.OnlyOnFaulted
        );
    }

    public async Task<IReadOnlyCollection<CompletionItemWithDescription>> CompleteAsync(
        string text,
        int caret
    )
    {
        if (!Initialization.IsCompleted)
        {
            return Array.Empty<CompletionItemWithDescription>();
        }

        Document document = _workspaceManager.CurrentDocument.WithText(SourceText.From(text));

        return await _autocompleteService.Complete(document, text, caret)
            .ConfigureAwait(false);
    }

    public async Task<EvaluationResult> ConvertToIntermediateLanguage(
        string csharpCode,
        bool debugMode
    )
    {
        await Initialization.ConfigureAwait(false);

        return _disassembler.Disassemble(csharpCode, debugMode);
    }

    public async Task<EvaluationResult> EvaluateAsync(
        string input,
        string[]? args = null,
        CancellationToken cancellationToken = default
    )
    {
        await Initialization.ConfigureAwait(false);

        EvaluationResult result = await _scriptRunner
            .RunCompilation(input.Trim(), args, cancellationToken)
            .ConfigureAwait(false);

        if (result is EvaluationResult.Success success)
            // update our final document text, and add a new, empty project that can be
            // used for future evaluations (whether evaluation, syntax highlighting, or completion)
        {
            _workspaceManager!.UpdateCurrentDocument(success);
        }

        return result;
    }

    public async Task<SymbolResult> GetSymbolAtIndexAsync(string text, int caret)
    {
        await Initialization.ConfigureAwait(false);

        return await _symbolExplorer.LookupSymbolAtPosition(text, caret);
    }

    public async Task<bool> IsTextCompleteStatementAsync(string text)
    {
        if (!Initialization.IsCompleted)
        {
            return true;
        }

        Document document = _workspaceManager.CurrentDocument.WithText(SourceText.From(text));
        SyntaxNode? root = await document.GetSyntaxRootAsync()
            .ConfigureAwait(false);

        return
            root is null ||
            SyntaxFactory.IsCompleteSubmission(
                root.SyntaxTree
            ); // if something's wrong and we can't get the syntax tree, we don't want to prevent evaluation.
    }

    public async Task<string?> PrettyPrintAsync(object? obj, bool displayDetails)
    {
        await Initialization.ConfigureAwait(false);

        return obj is Exception ex
            ? _prettyPrinter.FormatException(ex, displayDetails)
            : _prettyPrinter.FormatObject(obj, displayDetails);
    }

    public async Task<IReadOnlyCollection<HighlightedSpan>> SyntaxHighlightAsync(string text)
    {
        if (!Initialization.IsCompleted)
        {
            return Array.Empty<HighlightedSpan>();
        }

        Document document = _workspaceManager.CurrentDocument.WithText(SourceText.From(text));
        IReadOnlyCollection<HighlightedSpan> highlighted
            = await _highlighter.HighlightAsync(document);

        return highlighted;
    }

    public AnsiColor ToColor(string keyword)
        => _highlighter.GetColor(keyword);

    /// <summary>
    ///     Roslyn services can be a bit slow to initialize the first time they're executed.
    ///     Warm them up in the background so it doesn't affect the user.
    /// </summary>
    public Task WarmUpAsync(string[] args)
    {
        return Task.Run(
            async () =>
            {
                await Initialization.ConfigureAwait(false);

                _logger.Log("Warm-up Starting");

                Task<EvaluationResult> evaluationTask = EvaluateAsync(@"_ = ""REPL Warmup""", args);
                Task<IReadOnlyCollection<HighlightedSpan>> highlightTask
                    = SyntaxHighlightAsync(@"_ = ""REPL Warmup""");
                Task<Task<string>> completionTask = Task.WhenAny(
                    (await CompleteAsync(@"C", 1)).Where(
                        completion => completion.Item.DisplayText.StartsWith("C")
                    )
                    .Take(15)
                    .Select(completion => completion.DescriptionProvider.Value)
                );

                await Task.WhenAll(evaluationTask, highlightTask, completionTask)
                    .ConfigureAwait(false);
                _logger.Log("Warm-up Complete");
            }
        );
    }

    private AutoCompleteService? _autocompleteService;
    private CSharpCompilationOptions? _compilationOptions;
    private Disassembler? _disassembler;
    private readonly SyntaxHighlighter _highlighter;
    private readonly ITraceLogger _logger;
    private PrettyPrinter? _prettyPrinter;
    private AssemblyReferenceService? _referenceService;
    private ScriptRunner? _scriptRunner;
    private SymbolExplorer? _symbolExplorer;
    private WorkspaceManager? _workspaceManager;
}
