// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;

namespace CSharpRepl.Services.Roslyn.Scripting;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using MetadataResolvers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.Scripting.Hosting;

using PrettyPrompt.Consoles;

using References;

/// <summary>
///     Uses the Roslyn Scripting APIs to execute C# code in a string.
/// </summary>
internal sealed class ScriptRunner
{
    public ScriptRunner(
        CSharpCompilationOptions compilationOptions,
        AssemblyReferenceService referenceAssemblyService,
        IConsole console
    )
    {
        _console = console;
        _referenceAssemblyService = referenceAssemblyService;
        _assemblyLoader = new InteractiveAssemblyLoader(new MetadataShadowCopyProvider());
        _nugetResolver = new NugetPackageMetadataResolver(console);

        _metadataResolver = new CompositeMetadataReferenceResolver(
            _nugetResolver,
            new ProjectFileMetadataResolver(console),
            new AssemblyReferenceMetadataResolver(console, referenceAssemblyService)
        );
        _scriptOptions = ScriptOptions.Default.WithMetadataResolver(_metadataResolver)
            .WithReferences(referenceAssemblyService.LoadedImplementationAssemblies)
            .WithAllowUnsafe(compilationOptions.AllowUnsafe)
            .AddImports(compilationOptions.Usings);
    }

    /// <summary>
    ///     Compiles the provided code, with references to all previous script evaluations.
    ///     However, the provided code is not run or persisted; future evaluations will not
    ///     know about the code provided to this method.
    /// </summary>
    public Compilation CompileTransient(string code, OptimizationLevel optimizationLevel)
        => CSharpCompilation.CreateScriptCompilation(
            "CompilationTransient",
            CSharpSyntaxTree.ParseText(
                code,
                CSharpParseOptions.Default.WithKind(SourceCodeKind.Script)
                    .WithLanguageVersion(LanguageVersion.Latest)
            ),
            _scriptOptions.MetadataReferences,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                usings: _scriptOptions.Imports,
                optimizationLevel: optimizationLevel,
                allowUnsafe: _scriptOptions.AllowUnsafe,
                metadataReferenceResolver: _metadataResolver
            ),
            _state?.Script.GetCompilation() is CSharpCompilation previous
                ? previous
                : null,
            globalsType: typeof(ScriptGlobals)
        );

    /// <summary>
    ///     Accepts a string containing C# code and runs it. Subsequent invocations will use the state from
    ///     earlier
    ///     invocations.
    /// </summary>
    public async Task<EvaluationResult> RunCompilation(
        string text,
        string[]? args = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            IEnumerable<string> nugetCommands = text.Split(
                    new[]
                    {
                        '\r', '\n',
                    },
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
                )
                .Where(_nugetResolver.IsNugetReference);

            foreach (string nugetCommand in nugetCommands)
            {
                ImmutableArray<PortableExecutableReference> assemblyReferences
                    = await _nugetResolver.InstallNugetPackageAsync(nugetCommand, cancellationToken)
                        .ConfigureAwait(false);
                _scriptOptions = _scriptOptions.AddReferences(assemblyReferences);
            }

            IReadOnlyCollection<UsingDirectiveSyntax> usings
                = _referenceAssemblyService.GetUsings(text);
            _referenceAssemblyService.TrackUsings(usings);

            _state = await EvaluateStringWithStateAsync(
                    text,
                    _state,
                    _assemblyLoader,
                    _scriptOptions,
                    args,
                    cancellationToken
                )
                .ConfigureAwait(false);

            return _state.Exception is null
                ? CreateSuccessfulResult(text, _state)
                : new EvaluationResult.Error(_state.Exception);
        }
        catch (Exception oce) when (oce is OperationCanceledException ||
                                    oce.InnerException is OperationCanceledException)
        {
            // user can cancel by pressing ctrl+c, which triggers the CancellationToken
            return new EvaluationResult.Cancelled();
        }
        catch (Exception exception)
        {
            return new EvaluationResult.Error(exception);
        }
    }

    private readonly InteractiveAssemblyLoader _assemblyLoader;
    private readonly IConsole _console;
    private readonly MetadataReferenceResolver _metadataResolver;
    private readonly NugetPackageMetadataResolver _nugetResolver;
    private readonly AssemblyReferenceService _referenceAssemblyService;
    private ScriptOptions _scriptOptions;
    private ScriptState<object>? _state;

    private ScriptGlobals CreateGlobalsObject(string[]? args)
        => new(_console, args ?? Array.Empty<string>());

    private EvaluationResult.Success CreateSuccessfulResult(string text, ScriptState<object> state)
    {
        _referenceAssemblyService.AddImplementationAssemblyReferences(
            state.Script.GetCompilation()
                .References
        );
        IReadOnlySet<MetadataReference> frameworkReferenceAssemblies
            = _referenceAssemblyService.LoadedReferenceAssemblies;
        IReadOnlySet<MetadataReference> frameworkImplementationAssemblies
            = _referenceAssemblyService.LoadedImplementationAssemblies;
        _scriptOptions = _scriptOptions.WithReferences(frameworkImplementationAssemblies);

        return new EvaluationResult.Success(
            text,
            state.ReturnValue,
            frameworkImplementationAssemblies.Concat(frameworkReferenceAssemblies)
                .ToList()
        );
    }

    private Task<ScriptState<object>> EvaluateStringWithStateAsync(
        string text,
        ScriptState<object>? state,
        InteractiveAssemblyLoader assemblyLoader,
        ScriptOptions scriptOptions,
        string[]? args = null,
        CancellationToken cancellationToken = default
    )
        => state is null
            ? CSharpScript.Create(
                    text,
                    scriptOptions,
                    typeof(ScriptGlobals),
                    assemblyLoader
                )
                .RunAsync(CreateGlobalsObject(args), cancellationToken)
            : state.ContinueWithAsync(text, scriptOptions, cancellationToken);
}
