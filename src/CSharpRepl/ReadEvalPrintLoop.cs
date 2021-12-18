using System;

namespace CSharpRepl;

using System.Linq;
using System.Threading.Tasks;

using PrettyPrompt;
using PrettyPrompt.Consoles;
using PrettyPrompt.Highlighting;

using PrettyPromptConfig;

using Services;
using Services.Roslyn;
using Services.Roslyn.Scripting;

/// <summary>
///     The core REPL; prints the welcome message, collects input with the <see cref="PrettyPrompt" />
///     library and
///     processes that input with <see cref="RoslynServices" />.
/// </summary>
internal sealed class ReadEvalPrintLoop
{
    private string Help => _prompt.HasUserOptedOutFromColor
        ? @"""help"""
        : AnsiEscapeCodes.Green + "help" + AnsiEscapeCodes.Reset;

    private string Exit => _prompt.HasUserOptedOutFromColor
        ? @"""exit"""
        : AnsiEscapeCodes.BrightRed + "exit" + AnsiEscapeCodes.Reset;

    public ReadEvalPrintLoop(RoslynServices roslyn, IPrompt prompt, IConsole console)
    {
        _roslyn = roslyn;
        _prompt = prompt;
        _console = console;
    }

    public async Task RunAsync(Configuration config)
    {
        _console.WriteLine("Welcome to the C# REPL (Read Eval Print Loop)!");
        _console.WriteLine(
            "Type C# expressions and statements at the prompt and press Enter to evaluate them."
        );
        _console.WriteLine($"Type {Help} to learn more, and type {Exit} to quit.");
        _console.WriteLine(string.Empty);

        await ReadEvalPrintLoop.Preload(_roslyn, _console, config)
            .ConfigureAwait(false);

        while (true)
        {
            PromptResult? response = await _prompt.ReadLineAsync("> ")
                .ConfigureAwait(false);

            if (response is ExitApplicationKeyPress)
            {
                break;
            }

            if (response.IsSuccess)
            {
                string commandText = response.Text.Trim()
                    .ToLowerInvariant();

                // evaluate built in commands
                if (commandText == "exit")
                {
                    break;
                }

                if (commandText == "clear")
                {
                    _console.Clear();

                    continue;
                }

                if (new[]
                    {
                        "help", "#help", "?",
                    }.Contains(commandText))
                {
                    PrintHelp();

                    continue;
                }

                // evaluate results returned by special keybindings (configured in the PromptConfiguration.cs)
                if (response is KeyPressCallbackResult callbackOutput)
                {
                    _console.WriteLine(Environment.NewLine + callbackOutput.Output);

                    continue;
                }

                response.CancellationToken.Register(() => Environment.Exit(1));

                // evaluate C# code and directives
                EvaluationResult result = await _roslyn.EvaluateAsync(
                        response.Text,
                        config.LoadScriptArgs,
                        response.CancellationToken
                    )
                    .ConfigureAwait(false);

                await ReadEvalPrintLoop.PrintAsync(
                    _roslyn,
                    _console,
                    result,
                    response.IsHardEnter
                );
            }
        }
    }

    private readonly IConsole _console;
    private readonly IPrompt _prompt;
    private readonly RoslynServices _roslyn;

    private string Color(string reference)
        => _prompt.HasUserOptedOutFromColor
            ? string.Empty
            : AnsiEscapeCodes.ToAnsiEscapeSequence(new ConsoleFormat(_roslyn!.ToColor(reference)));

    private static async Task Preload(RoslynServices roslyn, IConsole console, Configuration config)
    {
        bool hasReferences = config.References.Count > 0;
        bool hasLoadScript = config.LoadScript is not null;

        if (!hasReferences &&
            !hasLoadScript)
        {
            _ = roslyn.WarmUpAsync(
                config.LoadScriptArgs
            ); // don't await; we don't want to block the console while warmup happens.

            return;
        }

        if (hasReferences)
        {
            console.WriteLine("Adding supplied references...");
            string loadReferenceScript = string.Join(
                "\r\n",
                config.References.Select(reference => $@"#r ""{reference}""")
            );
            EvaluationResult loadReferenceScriptResult = await roslyn
                .EvaluateAsync(loadReferenceScript)
                .ConfigureAwait(false);
            await ReadEvalPrintLoop.PrintAsync(
                    roslyn,
                    console,
                    loadReferenceScriptResult,
                    false
                )
                .ConfigureAwait(false);
        }

        if (hasLoadScript)
        {
            console.WriteLine("Running supplied CSX file...");
            EvaluationResult loadScriptResult = await roslyn.EvaluateAsync(
                    config.LoadScript!,
                    config.LoadScriptArgs
                )
                .ConfigureAwait(false);
            await ReadEvalPrintLoop.PrintAsync(
                    roslyn,
                    console,
                    loadScriptResult,
                    false
                )
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Produce syntax-highlighted strings like "#r reference" for the provided
    ///     <paramref name="argument" /> string.
    /// </summary>
    private string Preprocessor(string keyword, string? argument = null)
    {
        string highlightedKeyword = Color("preprocessor keyword") + keyword + AnsiEscapeCodes.Reset;
        string highlightedArgument = argument is null
            ? ""
            : Color("string") + @" """ + argument + @"""" + AnsiEscapeCodes.Reset;

        return highlightedKeyword + highlightedArgument;
    }

    private static async Task PrintAsync(
        RoslynServices roslyn,
        IConsole console,
        EvaluationResult result,
        bool displayDetails
    )
    {
        switch (result)
        {
            case EvaluationResult.Success ok:
                string? formatted = await roslyn.PrettyPrintAsync(ok?.ReturnValue, displayDetails);
                console.WriteLine(formatted);

                break;

            case EvaluationResult.Error err:
                string? formattedError
                    = await roslyn.PrettyPrintAsync(err.Exception, displayDetails);
                console.WriteErrorLine(
                    AnsiEscapeCodes.Red + formattedError + AnsiEscapeCodes.Reset
                );

                break;

            case EvaluationResult.Cancelled:
                console.WriteErrorLine(
                    AnsiEscapeCodes.Yellow + "Operation cancelled." + AnsiEscapeCodes.Reset
                );

                break;
        }
    }

    private void PrintHelp()
    {
        _console.WriteLine(
            $@"
More details and screenshots are available at
https://github.com/waf/CSharpRepl/blob/main/README.md

Evaluating Code
===============
Type C# at the prompt and press {ReadEvalPrintLoop.Underline("Enter")} to run it. The result will be printed.
{ReadEvalPrintLoop.Underline("Ctrl+Enter")} will also run the code, but show detailed member info / stack traces.
{ReadEvalPrintLoop.Underline("Shift+Enter")} will insert a newline, to support multiple lines of input.
If the code isn't a complete statement, pressing Enter will insert a newline.

Adding References
=================
Use the {Reference()} command to add assembly or nuget references.
For assembly references, run {Reference("AssemblyName")} or {Reference("path/to/assembly.dll")}
For nuget packages, run {Reference("nuget: PackageName")} or {Reference("nuget: PackageName, version")}
For project references, run {Reference("path/to/my.csproj")} or {Reference("path/to/my.sln")} 

Use {Preprocessor("#load", "path-to-file")} to evaluate C# stored in files (e.g. csx files). This can
be useful, for example, to build a "".profile.csx"" that includes libraries you want
to load.

Exploring Code
==============
{ReadEvalPrintLoop.Underline("F1")}: when the caret is in a type or member, open the corresponding MSDN documentation.
{ReadEvalPrintLoop.Underline("F9")}: show the IL (intermediate language) for the current statement.
{ReadEvalPrintLoop.Underline("F12")}: open the type's source code in the browser, if the assembly supports Source Link.

Configuration Options
=====================
All configuration, including theming, is done at startup via command line flags.
Run --help at the command line to view these options
"
        );
    }

    private string Reference(string? argument = null)
        => Preprocessor("#r", argument);

    private static string Underline(string word)
        => AnsiEscapeCodes.ToAnsiEscapeSequence(new ConsoleFormat(Underline: true)) +
           word +
           AnsiEscapeCodes.Reset;
}
