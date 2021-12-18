// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;

namespace CSharpRepl.PrettyPromptConfig;

using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

using PrettyPrompt;
using PrettyPrompt.Completion;
using PrettyPrompt.Consoles;
using PrettyPrompt.Highlighting;

using Services.Completion;
using Services.Roslyn;
using Services.Roslyn.Scripting;
using Services.SymbolExploration;
using Services.SyntaxHighlighting;

internal static class PromptConfiguration
{
    /// <summary>
    ///     Create our callbacks for configuring <see cref="PrettyPrompt" />
    /// </summary>
    public static PromptCallbacks Configure(IConsole console, RoslynServices roslyn)
    {
        return new PromptCallbacks
        {
            CompletionCallback = CompletionHandler,
            HighlightCallback = HighlightHandler,
            ForceSoftEnterCallback = ForceSoftEnterHandler,
            KeyPressCallbacks =
            {
                [ConsoleKey.F1] = LaunchHelpForSymbol,
                [(ConsoleModifiers.Control, ConsoleKey.F1)] = LaunchSourceForSymbol,
                [ConsoleKey.F9] = DisassembleDebug,
                [(ConsoleModifiers.Control, ConsoleKey.F9)] = DisassembleRelease,
                [ConsoleKey.F12] = LaunchSourceForSymbol,
                [(ConsoleModifiers.Control, ConsoleKey.D)] = ExitApplication,
            },
        };

        async Task<IReadOnlyList<CompletionItem>> CompletionHandler(string text, int caret)
            => PromptConfiguration.AdaptCompletions(
                await roslyn.CompleteAsync(text, caret)
                    .ConfigureAwait(false)
            );

        async Task<IReadOnlyCollection<FormatSpan>> HighlightHandler(string text)
            => PromptConfiguration.AdaptSyntaxClassification(
                await roslyn.SyntaxHighlightAsync(text)
                    .ConfigureAwait(false)
            );

        async Task<bool> ForceSoftEnterHandler(string text)
            => !await roslyn.IsTextCompleteStatementAsync(text)
                .ConfigureAwait(false);

        async Task<KeyPressCallbackResult?> LaunchHelpForSymbol(string text, int caret)
            => PromptConfiguration.LaunchDocumentation(
                await roslyn.GetSymbolAtIndexAsync(text, caret)
            );

        async Task<KeyPressCallbackResult?> LaunchSourceForSymbol(string text, int caret)
            => PromptConfiguration.LaunchSource(
                await roslyn.GetSymbolAtIndexAsync(text, caret)
            );

        Task<KeyPressCallbackResult?> DisassembleDebug(string text, int caret)
            => PromptConfiguration.Disassemble(
                roslyn,
                text,
                console,
                true
            );

        Task<KeyPressCallbackResult?> DisassembleRelease(string text, int caret)
            => PromptConfiguration.Disassemble(
                roslyn,
                text,
                console,
                false
            );

        Task<KeyPressCallbackResult?> ExitApplication(string text, int caret)
            => Task.FromResult<KeyPressCallbackResult?>(new ExitApplicationKeyPress());
    }

    private static IReadOnlyList<CompletionItem> AdaptCompletions(
        IReadOnlyCollection<CompletionItemWithDescription> completions
    )
    {
        return completions.OrderByDescending(i => i.Item.Rules.MatchPriority)
            .Select(
                r => new CompletionItem
                {
                    StartIndex = r.Item.Span.Start,
                    ReplacementText = r.Item.DisplayText,
                    DisplayText = r.Item.DisplayTextPrefix +
                                  r.Item.DisplayText +
                                  r.Item.DisplayTextSuffix,
                    ExtendedDescription = r.DescriptionProvider,
                }
            )
            .ToArray();
    }

    private static IReadOnlyCollection<FormatSpan> AdaptSyntaxClassification(
        IReadOnlyCollection<HighlightedSpan> classifications
    )
        => classifications.ToFormatSpans();

    private static async Task<KeyPressCallbackResult?> Disassemble(
        RoslynServices roslyn,
        string text,
        IConsole console,
        bool debugMode
    )
    {
        EvaluationResult result = await roslyn.ConvertToIntermediateLanguage(text, debugMode);

        switch (result)
        {
            case EvaluationResult.Success success:
                var ilCode = success.ReturnValue.ToString()!;
                IReadOnlyCollection<HighlightedSpan> highlighting = await roslyn
                    .SyntaxHighlightAsync(ilCode)
                    .ConfigureAwait(false);
                string? syntaxHighlightedOutput = Prompt.RenderAnsiOutput(
                    ilCode,
                    highlighting.ToFormatSpans(),
                    console.BufferWidth
                );

                return new KeyPressCallbackResult(text, syntaxHighlightedOutput);

            case EvaluationResult.Error err:
                return new KeyPressCallbackResult(
                    text,
                    AnsiEscapeCodes.Red + err.Exception.Message + AnsiEscapeCodes.Reset
                );

            default:
                // this should never happen, as the disassembler cannot be cancelled.
                throw new InvalidOperationException("Could not process disassembly result");
        }
    }

    private static KeyPressCallbackResult? LaunchBrowser(string url)
    {
        string opener = OperatingSystem.IsWindows()
            ? "explorer"
            : OperatingSystem.IsMacOS()
                ? "open"
                : "xdg-open";

        Process? browser
            = Process.Start(
                new ProcessStartInfo(opener, '"' + url + '"')
            ); // wrap in quotes so we can pass through url hashes (#)
        browser?.WaitForExit(); // wait for exit seems to make this work better on WSL2.

        return null;
    }

    private static KeyPressCallbackResult? LaunchDocumentation(SymbolResult type)
    {
        if ((type != SymbolResult.Unknown) &&
            type.SymbolDisplay is not null)
        {
            string culture = CultureInfo.CurrentCulture.Name;
            PromptConfiguration.LaunchBrowser(
                $"https://docs.microsoft.com/{culture}/dotnet/api/{type.SymbolDisplay}"
            );
        }

        return null;
    }

    private static KeyPressCallbackResult? LaunchSource(SymbolResult type)
    {
        if (type.Url is not null)
        {
            PromptConfiguration.LaunchBrowser(type.Url);
        }
        else if ((type != SymbolResult.Unknown) &&
                 type.SymbolDisplay is not null)
        {
            PromptConfiguration.LaunchBrowser($"https://source.dot.net/#q={type.SymbolDisplay}");
        }

        return null;
    }
}

/// <summary>
///     Used when the user presses an "exit application" key combo (ctrl-d) to instruct the main REPL
///     loop to end.
/// </summary>
internal sealed record ExitApplicationKeyPress() : KeyPressCallbackResult(null, null);
