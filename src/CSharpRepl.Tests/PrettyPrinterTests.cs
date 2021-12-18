namespace CSharpRepl.Tests;

using System;
using System.Collections.Generic;

using Services.Roslyn;

using Xunit;

public class PrettyPrinterTests
{
    [ Theory, MemberData(nameof(PrettyPrinterTests.FormatObjectInputs)), ]
    public void FormatObject_ObjectInput_PrintsOutput(
        object obj,
        bool showDetails,
        string expectedResult
    )
    {
        string prettyPrinted = new PrettyPrinter().FormatObject(obj, showDetails);
        Assert.Equal(expectedResult, prettyPrinted);
    }

    public static IEnumerable<object[]> FormatObjectInputs = new[]
    {
        new object[]
        {
            null, false, null,
        },
        new object[]
        {
            null, true, null,
        },
        new object[]
        {
            @"""hello world""", false, @"""\""hello world\""""",
        },
        new object[]
        {
            @"""hello world""", true, @"""hello world""",
        },
        new object[]
        {
            "a\nb", false, @"""a\nb""",
        },
        new object[]
        {
            "a\nb", true, "a\nb",
        },
        new object[]
        {
            new[]
            {
                1, 2, 3,
            },
            false,
            "int[3] { 1, 2, 3 }",
        },
        new object[]
        {
            new[]
            {
                1, 2, 3,
            },
            true,
            $"int[3] {"{"}{Environment.NewLine}  1,{Environment.NewLine}  2,{Environment.NewLine}  3{Environment.NewLine}{"}"}{Environment.NewLine}",
        },
    };
}
