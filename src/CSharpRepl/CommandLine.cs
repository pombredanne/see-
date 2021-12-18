// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;

namespace CSharpRepl;

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

using Services;
using Services.Roslyn.References;

/// <summary>
///     Parses command line arguments using System.CommandLine.
///     Includes support for dotnet-suggest.
/// </summary>
internal static class CommandLine
{
    public static Configuration Parse(string[] args)
    {
        string[] parseArgs = CommandLine.RemoveScriptArguments(args)
            .ToArray();

        CommandLine.Framework.AddValidator(
            r =>
            {
                if (!r.Children.Any())
                {
                    return null;
                }

                string frameworkValue = r.GetValueOrDefault<string>() ?? string.Empty;

                return SharedFramework.SupportedFrameworks.Any(
                    f => frameworkValue.StartsWith(f, StringComparison.OrdinalIgnoreCase)
                )
                    ? null // success
                    : "Unrecognized --framework value";
            }
        );

        ParseResult commandLine = new CommandLineBuilder(
                new RootCommand("C# REPL")
                {
                    CommandLine.References,
                    CommandLine.Usings,
                    CommandLine.Framework,
                    CommandLine.Theme,
                    CommandLine.Trace,
                    CommandLine.Help,
                    CommandLine.Version,
                }
            ).UseSuggestDirective() // support autocompletion via dotnet-suggest
            .Build()
            .Parse(parseArgs);

        if (CommandLine.ShouldExitEarly(commandLine, out string? text))
        {
            return new Configuration
            {
                OutputForEarlyExit = text,
            };
        }

        if (commandLine.Errors.Count > 0)
        {
            throw new InvalidOperationException(
                string.Join(Environment.NewLine, commandLine.Errors)
            );
        }

        Configuration config = new()
        {
            References = commandLine.ValueForOption(CommandLine.References)
                             ?.ToHashSet() ??
                         new HashSet<string>(),
            Usings = commandLine.ValueForOption(CommandLine.Usings)
                         ?.ToHashSet() ??
                     new HashSet<string>(),
            Framework = commandLine.ValueForOption(CommandLine.Framework) ??
                        Configuration.FRAMEWORK_DEFAULT,
            LoadScript = CommandLine.ProcessScriptArguments(args),
            LoadScriptArgs = commandLine.UnparsedTokens.ToArray(),
            Theme = commandLine.ValueForOption(CommandLine.Theme),
            Trace = commandLine.ValueForOption(CommandLine.Trace),
        };

        return config;
    }

    private const string DISABLE_FURTHER_OPTION_PARSING = "--";

    private static readonly Option<string[]> References = new(
        new[]
        {
            "--reference", "-r", "/r",
        },
        description: "Reference assemblies, nuget packages, and csproj files.",
        getDefaultValue: Array.Empty<string>
    );

    private static readonly Option<string[]> Usings = new Option<string[]>(
        new[]
        {
            "--using", "-u", "/u",
        },
        description: "Add using statements.",
        getDefaultValue: Array.Empty<string>
    ).AddSuggestions(CommandLine.GetAvailableUsings);

    private static readonly Option<string> Framework = new Option<string>(
        new[]
        {
            "--framework", "-f", "/f",
        },
        description: "Reference a shared framework.",
        getDefaultValue: () => Configuration.FRAMEWORK_DEFAULT
    ).AddSuggestions(SharedFramework.SupportedFrameworks);

    /// <summary>
    ///     Autocompletions for --using.
    /// </summary>
    private static IEnumerable<string> GetAvailableUsings(
        ParseResult? parseResult,
        string? textToMatch
    )
    {
        if (string.IsNullOrEmpty(textToMatch) ||
            "Syste".StartsWith(textToMatch, StringComparison.OrdinalIgnoreCase))
        {
            return new[]
            {
                "System",
            };
        }

        if (!textToMatch.StartsWith("System", StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<string>();
        }

        string[]? runtimeAssemblyPaths = Directory.GetFiles(
            RuntimeEnvironment.GetRuntimeDirectory(),
            "*.dll"
        );
        using MetadataLoadContext? mlc = new(new PathAssemblyResolver(runtimeAssemblyPaths));

        IEnumerable<string>? namespaces =
            from assembly in runtimeAssemblyPaths
            from type in GetTypes(assembly)
            where type.IsPublic &&
                  type.Namespace is not null &&
                  type.Namespace.StartsWith(textToMatch, StringComparison.OrdinalIgnoreCase)
            select type.Namespace;

        return namespaces.Distinct()
            .Take(16)
            .ToArray();

        IEnumerable<Type> GetTypes(string assemblyPath)
        {
            try
            {
                return mlc.LoadFromAssemblyPath(assemblyPath)
                    .GetTypes();
            }
            catch (BadImageFormatException)
            {
                return Array.Empty<Type>();
            } // handle native DLLs that have no managed metadata.
        }
    }

    /// <summary>
    ///     Output of --help
    /// </summary>
    /// <remarks>
    ///     System.CommandLine can generate the help text for us, but I think it's less
    ///     readable, and the code to configure it ends up being longer than the below string.
    /// </remarks>
    private static string GetHelp()
        => CommandLine.GetVersion() +
           Environment.NewLine +
           "Usage: csharprepl [OPTIONS] [@response-file.rsp] [script-file.csx] [-- <additional-arguments>]" +
           Environment.NewLine +
           Environment.NewLine +
           "Starts a REPL (read eval print loop) according to the provided [OPTIONS]." +
           Environment.NewLine +
           "These [OPTIONS] can be provided at the command line, or via a [@response-file.rsp]." +
           Environment.NewLine +
           "A [script-file.csx], if provided, will be executed before the prompt starts." +
           Environment.NewLine +
           Environment.NewLine +
           "OPTIONS:" +
           Environment.NewLine +
           "  -r <dll> or --reference <dll>:             Reference assemblies, nuget packages, and csproj files." +
           Environment.NewLine +
           "  -u <namespace> or --using <namespace>:     Add using statements." +
           Environment.NewLine +
           "  -f <framework> or --framework <framework>: Reference a shared framework." +
           Environment.NewLine +
           "                                             Available shared frameworks: " +
           Environment.NewLine +
           CommandLine.GetInstalledFrameworks("                                             ") +
           Environment.NewLine +
           "  -t <theme.json> or --theme <theme.json>:   Read a theme file for syntax highlighting. Respects the NO_COLOR standard." +
           Environment.NewLine +
           "  -v or --version:                           Show version number and exit." +
           Environment.NewLine +
           "  -h or --help:                              Show this help and exit." +
           Environment.NewLine +
           "  --trace:                                   Produce a trace file in the current directory, for CSharpRepl bug reports." +
           Environment.NewLine +
           Environment.NewLine +
           "@response-file.rsp:" +
           Environment.NewLine +
           "  A file, with extension .rsp, containing the above command line [OPTIONS], one option per line." +
           Environment.NewLine +
           Environment.NewLine +
           "script-file.csx:" +
           Environment.NewLine +
           "  A file, with extension .csx, containing lines of C# to evaluate before starting the REPL." +
           Environment.NewLine +
           "  Arguments to this script can be passed as <additional-arguments> and will be available in a global `args` variable." +
           Environment.NewLine;

    /// <summary>
    ///     In the help text, lists the available frameworks and marks one as default.
    /// </summary>
    private static string GetInstalledFrameworks(string leftPadding)
    {
        IEnumerable<string> frameworkList = SharedFramework.SupportedFrameworks.Select(
            fx => leftPadding +
                  "- " +
                  fx +
                  (fx == Configuration.FRAMEWORK_DEFAULT
                      ? " (default)"
                      : "")
        );

        return string.Join(Environment.NewLine, frameworkList);
    }

    /// <summary>
    ///     Get assembly version for usage in --version
    /// </summary>
    private static string GetVersion()
    {
        var product = "C# REPL";
        string version = Assembly.GetExecutingAssembly()
                             .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                             ?.InformationalVersion ??
                         "unversioned";

        return product + " " + version;
    }

    private static readonly Option<bool> Help = new(
        new[]
        {
            "--help", "-h", "-?", "/h", "/?",
        },
        "Show this help and exit."
    );

    /// <summary>
    ///     Reads the contents of any provided script (csx) files.
    /// </summary>
    private static string? ProcessScriptArguments(string[] args)
    {
        StringBuilder stringBuilder = new();

        foreach (string arg in args)
        {
            if (arg == CommandLine.DISABLE_FURTHER_OPTION_PARSING)
            {
                break;
            }

            if (!arg.EndsWith(".csx"))
            {
                continue;
            }

            if (!File.Exists(arg))
            {
                throw new FileNotFoundException($@"Script file ""{arg}"" was not found");
            }

            stringBuilder.AppendLine(File.ReadAllText(arg));
        }

        return stringBuilder.Length == 0
            ? null
            : stringBuilder.ToString();
    }

    /// <summary>
    ///     We allow csx files to be specified, sometimes in ambiguous scenarios that
    ///     System.CommandLine can't figure out. So we remove it from processing here,
    ///     and process it manually in <see cref="ProcessScriptArguments" />.
    /// </summary>
    private static IEnumerable<string> RemoveScriptArguments(string[] args)
    {
        var foundIgnore = false;

        foreach (string arg in args)
        {
            foundIgnore |= arg == CommandLine.DISABLE_FURTHER_OPTION_PARSING;

            if (foundIgnore || !arg.EndsWith(".csx"))
            {
                yield return arg;
            }
        }
    }

    private static bool ShouldExitEarly(
        ParseResult commandLine,
        [ NotNullWhen(true) ] out string? text
    )
    {
        if (commandLine.Directives.Any())
        {
            // this is just for dotnet-suggest directive processing. Invoking should write to stdout
            // and should not start the REPL. It's a feature of System.CommandLine.
            TestConsole console = new();
            commandLine.Invoke(console);
            text = console.Out.ToString() ?? string.Empty;

            return true;
        }

        if (commandLine.ValueForOption<bool>("--help"))
        {
            text = CommandLine.GetHelp();

            return true;
        }

        if (commandLine.ValueForOption<bool>("--version"))
        {
            text = CommandLine.GetVersion();

            return true;
        }

        text = null;

        return false;
    }

    private static readonly Option<string> Theme = new(
        new[]
        {
            "--theme", "-t", "/t",
        },
        description: "Read a theme file for syntax highlighting. Respects the NO_COLOR standard.",
        getDefaultValue: () => string.Empty
    );

    private static readonly Option<bool> Trace = new(
        new[]
        {
            "--trace",
        },
        "Enable a trace log, written to the current directory."
    );

    private static readonly Option<bool> Version = new(
        new[]
        {
            "--version", "-v", "/v",
        },
        "Show version number and exit."
    );
}
