using System;

namespace CSharpRepl.Tests;

using System.IO;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using NSubstitute;

using PrettyPrompt.Consoles;

using Services;
using Services.Disassembly;
using Services.Roslyn;
using Services.Roslyn.References;
using Services.Roslyn.Scripting;

using Xunit;

[ Collection(nameof(RoslynServices)) ]
public class DisassemblerTests : IAsyncLifetime
{
    public Task DisposeAsync()
        => Task.CompletedTask;

    public Task InitializeAsync()
        => _services.WarmUpAsync(Array.Empty<string>());

    public DisassemblerTests()
    {
        CSharpCompilationOptions options = new(
            OutputKind.DynamicallyLinkedLibrary,
            usings: Array.Empty<string>()
        );
        IConsole console = Substitute.For<IConsole>();
        console.BufferWidth.Returns(200);
        AssemblyReferenceService referenceService = new(new Configuration(), new TestTraceLogger());
        ScriptRunner scriptRunner = new(options, referenceService, console);

        _disassembler = new Disassembler(options, referenceService, scriptRunner);
        _services = new RoslynServices(console, new Configuration(), new TestTraceLogger());
    }

    [ Fact ]
    public async Task Disassemble_ImportsAcrossMultipleReplLines_CanDisassemble()
    {
        // import a namespace
        await _services.EvaluateAsync("using System.Globalization;");

        // disassemble code that uses the above imported namespace.
        EvaluationResult result = await _services.ConvertToIntermediateLanguage(
            "var x = CultureInfo.CurrentCulture;",
            false
        );

        EvaluationResult.Success success = Assert.IsType<EvaluationResult.Success>(result);
        Assert.Contains(
            "Compiling code as Console Application (with top-level statements): succeeded",
            success.ReturnValue.ToString()
        );
    }

    [ Fact ]
    public async Task Disassemble_InputAcrossMultipleReplLines_CanDisassemble()
    {
        // define a variable
        await _services.EvaluateAsync("var x = 5;");

        // disassemble code that uses the above variable. This is an interesting case as the roslyn scripting will convert
        // the above local variable into a field, so it can be referenced by a subsequent script.
        EvaluationResult result
            = await _services.ConvertToIntermediateLanguage("Console.WriteLine(x)", false);

        EvaluationResult.Success success = Assert.IsType<EvaluationResult.Success>(result);
        Assert.Contains(
            "Compiling code as Scripting session (will be overly verbose): succeeded",
            success.ReturnValue.ToString()
        );
    }

    [ Theory, InlineData(OptimizationLevel.Debug, "TopLevelProgram"),
      InlineData(OptimizationLevel.Release, "TopLevelProgram"),
      InlineData(OptimizationLevel.Debug, "TypeDeclaration"),
      InlineData(OptimizationLevel.Release, "TypeDeclaration"), ]
    public void Disassemble_InputCSharp_OutputIL(
        OptimizationLevel optimizationLevel,
        string testCase
    )
    {
        string input = File.ReadAllText($"./Data/Disassembly/{testCase}.Input.txt")
            .Replace("\r\n", "\n");
        string expectedOutput = File
            .ReadAllText($"./Data/Disassembly/{testCase}.Output.{optimizationLevel}.il")
            .Replace("\r\n", "\n");

        EvaluationResult result = _disassembler.Disassemble(
            input,
            optimizationLevel == OptimizationLevel.Debug
        );
        var actualOutput = Assert.IsType<EvaluationResult.Success>(result)
            .ReturnValue.ToString();

        Assert.Equal(expectedOutput, actualOutput);
    }

    private readonly Disassembler _disassembler;
    private readonly RoslynServices _services;
}
