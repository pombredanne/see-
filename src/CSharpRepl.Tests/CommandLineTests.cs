﻿// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;

namespace CSharpRepl.Tests;

using Services;
using Services.Roslyn.References;

using Xunit;

public class CommandLineTests
{
    [ Fact ]
    public void ParseArguments_ComplexCommandLine_ProducesConfiguration()
    {
        Configuration result = CommandLineTests.Parse(
            "-t foo.json -u System.Linq System.Data -u Newtonsoft.Json --reference foo.dll -f Microsoft.AspNetCore.App --reference bar.dll baz.dll Data/LoadScript.csx"
        );
        Assert.NotNull(result);
        Assert.Equal("foo.json", result.Theme);
        Assert.Equal(
            new[]
            {
                "System.Linq", "System.Data", "Newtonsoft.Json",
            },
            result.Usings
        );
        Assert.Equal(
            new[]
            {
                "foo.dll", "bar.dll", "baz.dll",
            },
            result.References
        );
        Assert.Equal("Microsoft.AspNetCore.App", result.Framework);
        Assert.Equal(
            @"Console.WriteLine(""Hello World!"");" + Environment.NewLine,
            result.LoadScript
        );
    }

    [ Fact ]
    public void ParseArguments_DotNetSuggestFrameworkParameter_IsAutocompleted()
    {
        Configuration result = CommandLineTests.Parse("[suggest:3] --f");
        Assert.Equal("--framework" + Environment.NewLine, result.OutputForEarlyExit);
    }

    [ Fact ]
    public void ParseArguments_DotNetSuggestFrameworkValue_IsAutocompleted()
    {
        Configuration result = CommandLine.Parse(
            new[]
            {
                "[suggest:12]", "--framework ",
            }
        );
        Assert.Contains("Microsoft.NETCore.App", result.OutputForEarlyExit);
    }

    [ Fact ]
    public void ParseArguments_DotNetSuggestUsingValue_IsAutocompleted()
    {
        Configuration result = CommandLine.Parse(
            new[]
            {
                "[suggest:25]", "--using System.Collection",
            }
        );
        Assert.Contains("System.Collections.Immutable", result.OutputForEarlyExit);
    }

    [ Theory, InlineData("-f"), InlineData("--framework"), InlineData("/f"), ]
    public void ParseArguments_FrameworkArgument_SpecifiesFramework(string flag)
    {
        Configuration result = CommandLineTests.Parse($"{flag} Microsoft.AspNetCore.App");
        Assert.NotNull(result);
        Assert.Equal("Microsoft.AspNetCore.App", result.Framework);

        Configuration concatenatedValue = CommandLineTests.Parse("/f:Microsoft.AspNetCore.App");
        Assert.Equal("Microsoft.AspNetCore.App", concatenatedValue.Framework);
    }

    [ Theory, InlineData("-h"), InlineData("--help"), InlineData("/h"), InlineData("/?"), ]
    public void ParseArguments_HelpArguments_SpecifiesHelp(string flag)
    {
        Configuration result = CommandLineTests.Parse(flag);
        Assert.NotNull(result);
        Assert.Contains("Usage: ", result.OutputForEarlyExit);
    }

    [ Fact ]
    public void ParseArguments_NoArguments_ProducesDefaultConfiguration()
    {
        Configuration result = CommandLineTests.Parse(null);
        Assert.NotNull(result);
        Assert.Equal(SharedFramework.NET_CORE_APP, result.Framework);
    }

    [ Theory, InlineData("-r"), InlineData("--reference"), InlineData("/r"), ]
    public void ParseArguments_ReferencesArguments_ProducesUsings(string flag)
    {
        Configuration result = CommandLineTests.Parse($"{flag} Foo.dll Bar.dll");
        Assert.NotNull(result);
        Assert.Equal(
            new[]
            {
                "Foo.dll", "Bar.dll",
            },
            result.References
        );

        Configuration concatenatedValues = CommandLineTests.Parse("/r:Foo.dll /r:Bar.dll");
        Assert.Equal(
            new[]
            {
                "Foo.dll", "Bar.dll",
            },
            concatenatedValues.References
        );
    }

    [ Fact ]
    public void ParseArguments_ResponseFile_ProducesConfiguration()
    {
        Configuration result = CommandLineTests.Parse("@Data/ResponseFile.rsp");

        Assert.NotNull(result);
        Assert.Equal(SharedFramework.NET_CORE_APP, result.Framework);
        Assert.Equal(
            new[]
            {
                "System", "System.Linq", "Foo.Main.Text",
            },
            result.Usings
        );
        Assert.Equal(
            new[]
            {
                "System", "System.ValueTuple.dll", "Foo.Main.Logic.dll", "lib.dll",
            },
            result.References
        );
    }

    [ Theory, InlineData("-t"), InlineData("--theme"), InlineData("/t"), ]
    public void ParseArguments_ThemeArguments_SpecifiesTheme(string flag)
    {
        Configuration result = CommandLineTests.Parse($"{flag} beautiful.json");
        Assert.NotNull(result);
        Assert.Equal("beautiful.json", result.Theme);
    }

    [ Fact ]
    public void ParseArguments_TrailingArgumentsAfterDoubleDash_SetAsLoadScriptArgs()
    {
        Configuration csxResult = CommandLine.Parse(
            new[]
            {
                "Data/LoadScript.csx", "--", "Data/LoadScript.csx",
            }
        );
        // load script filename passed before "--" is a load script, after "--" we just pass it to the load script as an arg.
        Assert.Equal(
            new[]
            {
                "Data/LoadScript.csx",
            },
            csxResult.LoadScriptArgs
        );
        Assert.Equal(
            @"Console.WriteLine(""Hello World!"");" + Environment.NewLine,
            csxResult.LoadScript
        );

        Configuration quotedResult = CommandLine.Parse(
            new[]
            {
                "-r", "Foo.dll", "--", @"""a b c""", @"""d e f""",
            }
        );
        Assert.Equal(
            new[]
            {
                @"""a b c""", @"""d e f""",
            },
            quotedResult.LoadScriptArgs
        );
        Assert.Equal(
            new[]
            {
                @"Foo.dll",
            },
            quotedResult.References
        );
    }

    [ Theory, InlineData("-u"), InlineData("--using"), InlineData("/u"), ]
    public void ParseArguments_UsingArguments_ProducesUsings(string flag)
    {
        Configuration result
            = CommandLineTests.Parse($"{flag} System.Linq System.Data Newtonsoft.Json");
        Assert.NotNull(result);
        Assert.Equal(
            new[]
            {
                "System.Linq", "System.Data", "Newtonsoft.Json",
            },
            result.Usings
        );

        Configuration concatenatedValues
            = CommandLineTests.Parse("/u:System.Linq /u:System.Data /u:Newtonsoft.Json");
        Assert.Equal(
            new[]
            {
                "System.Linq", "System.Data", "Newtonsoft.Json",
            },
            concatenatedValues.Usings
        );
    }

    [ Theory, InlineData("-v"), InlineData("--version"), InlineData("/v"), ]
    public void ParseArguments_VersionArguments_SpecifiesVersion(string flag)
    {
        Configuration result = CommandLineTests.Parse(flag);
        Assert.NotNull(result);
        Assert.Contains("C# REPL ", result.OutputForEarlyExit);
    }

    private static Configuration Parse(string commandline)
        => CommandLine.Parse(commandline?.Split(' ') ?? Array.Empty<string>());
}
