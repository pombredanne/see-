namespace CSharpRepl.Tests;

using System.Threading.Tasks;

using NSubstitute;
using NSubstitute.ClearExtensions;

using PrettyPrompt;
using PrettyPrompt.Consoles;

using PrettyPromptConfig;

using Services;
using Services.Roslyn;

using Xunit;

[ Collection(nameof(RoslynServices)) ]
public class ReadEvalPrintLoopTests : IClassFixture<RoslynServicesFixture>
{
    public ReadEvalPrintLoopTests(RoslynServicesFixture fixture)
    {
        _console = fixture.ConsoleStub;
        _prompt = fixture.PromptStub;
        _services = fixture.RoslynServices;
        _repl = new ReadEvalPrintLoop(_services, _prompt, _console);

        _console.ClearSubstitute();
        _prompt.ClearSubstitute();
    }

    [ Fact ]
    public async Task RunAsync_ClearCommand_ClearsScreen()
    {
        _prompt.ReadLineAsync("> ")
            .Returns(new PromptResult(true, "clear", false), new PromptResult(true, "exit", false));

        await _repl.RunAsync(new Configuration());

        _console.Received()
            .Clear();
    }

    [ Fact ]
    public async Task RunAsync_EvaluateCode_ReturnsResult()
    {
        _prompt.ReadLineAsync("> ")
            .Returns(new PromptResult(true, "5 + 3", false), new PromptResult(true, "exit", false));

        await _repl.RunAsync(new Configuration());

        _console.Received()
            .WriteLine("8");
    }

    [ Fact ]
    public async Task RunAsync_Exception_ShowsMessage()
    {
        _prompt.ReadLineAsync("> ")
            .Returns(
                new PromptResult(true, @"throw new InvalidOperationException(""bonk!"");", false),
                new PromptResult(true, "exit", false)
            );

        await _repl.RunAsync(new Configuration());

        _console.Received()
            .WriteErrorLine(Arg.Is<string>(message => message.Contains("bonk")));
    }

    [ Fact ]
    public async Task RunAsync_ExitCommand_ExitsRepl()
    {
        _prompt.ReadLineAsync("> ")
            .Returns(new ExitApplicationKeyPress());

        await _repl.RunAsync(new Configuration());

        // by reaching here, the application correctly exited.
    }

    [ Theory, InlineData("help"), InlineData("#help"), InlineData("?"), ]
    public async Task RunAsync_HelpCommand_ShowsHelp(string help)
    {
        _prompt.ReadLineAsync("> ")
            .Returns(new PromptResult(true, help, false), new PromptResult(true, "exit", false));

        await _repl.RunAsync(new Configuration());

        _console.Received()
            .WriteLine(Arg.Is<string>(str => str.Contains("Welcome to the C# REPL")));
        _console.Received()
            .WriteLine(Arg.Is<string>(str => str.Contains("Type C# at the prompt")));
    }

    [ Fact ]
    public async Task RunAsync_LoadScript_RunsScript()
    {
        _prompt.ReadLineAsync("> ")
            .Returns(new PromptResult(true, "x", false), new PromptResult(true, "exit", false));

        await _repl.RunAsync(
            new Configuration
            {
                LoadScript = @"var x = ""Hello World"";",
            }
        );

        _console.Received()
            .WriteLine(@"""Hello World""");
    }

    [ Fact ]
    public async Task RunAsync_Reference_AddsReference()
    {
        _prompt.ReadLineAsync("> ")
            .Returns(
                new PromptResult(true, "DemoLibrary.DemoClass.Multiply(5, 6)", false),
                new PromptResult(true, "exit", false)
            );

        await _repl.RunAsync(
            new Configuration
            {
                References =
                {
                    "Data/DemoLibrary.dll",
                },
            }
        );

        _console.Received()
            .WriteLine("30");
    }

    private readonly IConsole _console;
    private readonly IPrompt _prompt;
    private readonly ReadEvalPrintLoop _repl;
    private readonly RoslynServices _services;
}
