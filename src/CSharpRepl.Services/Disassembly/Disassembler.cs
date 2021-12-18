using System;

namespace CSharpRepl.Services.Disassembly;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Threading;

using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Disassembler;
using ICSharpCode.Decompiler.Metadata;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

using Roslyn.References;
using Roslyn.Scripting;

/// <summary>
///     Shows the IL code for the user's C# code.
/// </summary>
internal class Disassembler
{
    public Disassembler(
        CSharpCompilationOptions compilationOptions,
        AssemblyReferenceService referenceService,
        ScriptRunner scriptRunner
    )
    {
        _compilationOptions = compilationOptions;
        _referenceService = referenceService;

        // we will try to compile the user's code several different ways. The first one that succeeds will be used.
        _compilers = new (string name, CompileDelegate compile)[]
        {
            // "console application" will work for standalone statements, due to C#'s top-level statement feature.
            (name: "Console Application (with top-level statements)",
                compile: (code, optimizationLevel) => Compile(
                    code,
                    optimizationLevel,
                    OutputKind.ConsoleApplication
                )),
            // "DLL" will work if the user doesn't have statements, for example, they're only defining types.
            (name: "DLL",
                compile: (code, optimizationLevel) => Compile(
                    code,
                    optimizationLevel,
                    OutputKind.DynamicallyLinkedLibrary
                )),
            // Compiling as a script will work for most other cases, but it's quite verbose so we use it as a last resort.
            (name: "Scripting session (will be overly verbose)",
                compile: (code, optimizationLevel)
                    => scriptRunner.CompileTransient(code, optimizationLevel)),
        };
    }

    public EvaluationResult Disassemble(string code, bool debugMode)
    {
        IEnumerable<string> usings = _referenceService.Usings.Select(
            u => u.NormalizeWhitespace()
                .ToString()
        );
        code = string.Join(Environment.NewLine, usings) + Environment.NewLine + code;

        OptimizationLevel optimizationLevel = debugMode
            ? OptimizationLevel.Debug
            : OptimizationLevel.Release;
        var commentFooter = new List<string>
        {
            $"// Disassembled in {optimizationLevel} Mode." +
            (optimizationLevel == OptimizationLevel.Debug
                ? " Press Ctrl+F9 to disassemble in Release Mode."
                : string.Empty),
        };

        // the disassembler will write to the ilCodeOutput variable when invoked.
        PlainTextOutput ilCodeOutput = new()
        {
            IndentationString = new string(' ', 4),
        };
        ReflectionDisassembler disassembler = new(ilCodeOutput, CancellationToken.None);

        using MemoryStream stream = new();

        foreach ((string name, CompileDelegate compile) compiler in _compilers)
        {
            stream.SetLength(0);
            Compilation compiled = compiler.compile(code, optimizationLevel);
            EmitResult compilationResult = compiled.Emit(stream);

            if (compilationResult.Success)
            {
                commentFooter.Add($"// Compiling code as {compiler.name}: succeeded.");
                stream.Position = 0;
                PEFile file = new(
                    Guid.NewGuid()
                        .ToString(),
                    stream,
                    PEStreamOptions.LeaveOpen
                );
                disassembler.WriteModuleContents(file); // writes to the "ilCodeOutput" variable
                string ilCode = string.Join(
                                    '\n',
                                    ilCodeOutput.ToString()
                                        .Split(
                                            new[]
                                            {
                                                "\r\n", "\n",
                                            },
                                            StringSplitOptions.None
                                        )
                                        .Select(
                                            line => line.TrimEnd()
                                        ) // output has trailing spaces on some lines, clean those up
                                ) +
                                string.Join('\n', commentFooter);

                return new EvaluationResult.Success(code, ilCode, Array.Empty<MetadataReference>());
            }

            commentFooter.Add($"// Compiling code as {compiler.name}: failed.");
            commentFooter.AddRange(
                compilationResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(err => "//   - " + err.GetMessage())
            );
        }

        return new EvaluationResult.Error(
            new InvalidOperationException(
                "// Could not compile provided code:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, commentFooter)
            )
        );
    }

    private readonly CSharpCompilationOptions _compilationOptions;
    private readonly (string name, CompileDelegate compile)[] _compilers;
    private readonly AssemblyReferenceService _referenceService;

    private Compilation Compile(
        string code,
        OptimizationLevel optimizationLevel,
        OutputKind outputKind
    )
    {
        SyntaxTree ast = CSharpSyntaxTree.ParseText(
            code,
            new CSharpParseOptions(LanguageVersion.Latest)
        );
        CSharpCompilation compilation = CSharpCompilation.Create(
            "CompilationForDecompilation",
            new[]
            {
                ast,
            },
            _referenceService.LoadedReferenceAssemblies,
            _compilationOptions.WithOutputKind(outputKind)
                .WithOptimizationLevel(optimizationLevel)
                .WithUsings(_referenceService.Usings.Select(u => u.Name.ToString()))
        );

        return compilation;
    }

    private delegate Compilation CompileDelegate(string code, OptimizationLevel optimizationLevel);
}
