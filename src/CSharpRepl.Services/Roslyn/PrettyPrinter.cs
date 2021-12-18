// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;

namespace CSharpRepl.Services.Roslyn;

using Microsoft.CodeAnalysis.CSharp.Scripting.Hosting;
using Microsoft.CodeAnalysis.Scripting.Hosting;

internal sealed class PrettyPrinter
{
    public PrettyPrinter()
    {
        _formatter = CSharpObjectFormatter.Instance;
        _summaryOptions = new PrintOptions
        {
            MemberDisplayFormat = MemberDisplayFormat.SingleLine,
            MaximumOutputLength = 20_000,
        };
        _detailedOptions = new PrintOptions
        {
            MemberDisplayFormat = MemberDisplayFormat.SeparateLines,
            MaximumOutputLength = 20_000,
        };
    }

    public string FormatException(Exception obj, bool displayDetails)
        => displayDetails
            ? _formatter.FormatException(obj)
            : obj.Message;

    public string? FormatObject(object? obj, bool displayDetails)
    {
        return obj switch
        {
            null => null, // intercept null, don't print the string "null"
            string str when displayDetails =>
                str, // when displayDetails is true, don't show the escaped string (i.e. interpret the escape characters, via displaying to console)
            _ => _formatter.FormatObject(
                obj,
                displayDetails
                    ? _detailedOptions
                    : _summaryOptions
            ),
        };
    }

    private readonly PrintOptions _detailedOptions;
    private readonly ObjectFormatter _formatter;
    private readonly PrintOptions _summaryOptions;
}
