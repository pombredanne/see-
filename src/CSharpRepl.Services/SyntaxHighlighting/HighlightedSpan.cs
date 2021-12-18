﻿// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace CSharpRepl.Services.SyntaxHighlighting;

using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis.Text;

using PrettyPrompt.Highlighting;

public sealed record HighlightedSpan(TextSpan TextSpan, AnsiColor Color);

public static class HighlightedSpanExtensions
{
    public static IReadOnlyCollection<FormatSpan> ToFormatSpans(
        this IReadOnlyCollection<HighlightedSpan> spans
    )
    {
        return spans.Select(
                span => new FormatSpan(
                    span.TextSpan.Start,
                    span.TextSpan.Length,
                    new ConsoleFormat(span.Color)
                )
            )
            .Where(formatSpan => formatSpan.Formatting is not null)
            .ToArray();
    }
}
