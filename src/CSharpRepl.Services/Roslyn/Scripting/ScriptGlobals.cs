// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace CSharpRepl.Services.Roslyn.Scripting;

using System.Collections.Generic;

using Microsoft.CodeAnalysis.CSharp.Scripting.Hosting;

using PrettyPrompt.Consoles;

/// <summary>
///     Defines variables that are available in the C# Script environment.
/// </summary>
/// <remarks>Must be public so it can be referenced by the script</remarks>
public sealed class ScriptGlobals
{
#pragma warning disable IDE1006 // Naming Styles
    /// <summary>
    ///     Arguments provided at the command line after a double dash.
    ///     This naming convention matches top-level programs and Main method conventions.
    /// </summary>
    public string[] args { get; set; }
#pragma warning restore IDE1006 // Naming Styles

    /// <summary>
    ///     Arguments provided at the command line after a double dash.
    ///     This naming convention matches csi, dotnet-script, etc.
    /// </summary>
    public IList<string> Args { get; set; }

    public ScriptGlobals(IConsole console, string[] args)
    {
        _console = console;
        this.args = args;
        Args = new List<string>(args);
    }

    /// <summary>
    ///     Pretty-print a c# object. While not so useful in a REPL, where results
    ///     are pretty-printed by default, it's useful in CSX scripts.
    /// </summary>
    public void Print(object value)
    {
        _console.WriteLine(CSharpObjectFormatter.Instance.FormatObject(value));
    }

    private readonly IConsole _console;
}
