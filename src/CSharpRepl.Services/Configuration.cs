// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;

namespace CSharpRepl.Services;

using System.Collections.Generic;
using System.IO;

using Roslyn.References;

/// <summary>
///     Configuration from command line parameters
/// </summary>
public sealed class Configuration
{
    public HashSet<string> References { get; init; } = new();
    public HashSet<string> Usings { get; init; } = new();
    public string Framework { get; init; } = Configuration.FRAMEWORK_DEFAULT;

    public bool Trace { get; init; }
    public string? Theme { get; init; }
    public string? LoadScript { get; init; }
    public string[] LoadScriptArgs { get; init; } = Array.Empty<string>();

    public string? OutputForEarlyExit { get; init; }

    public static string ApplicationDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        ".csharprepl"
    );

    public static IReadOnlyCollection<string> SymbolServers
    {
        get
        {
            return new[]
            {
                "https://symbols.nuget.org/download/symbols/",
                "http://msdl.microsoft.com/download/symbols/",
            };
        }
    }

    public const string FRAMEWORK_DEFAULT = SharedFramework.NET_CORE_APP;
}
