// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;

namespace CSharpRepl.Tests;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Text;

using PrettyPrompt.Consoles;

using Services;
using Services.Roslyn;
using Services.SyntaxHighlighting;

using Xunit;

[ Collection(nameof(RoslynServices)) ]
public class SyntaxHighlightingTests : IAsyncLifetime
{
    public Task DisposeAsync()
        => Task.CompletedTask;

    public Task InitializeAsync()
        => _services.WarmUpAsync(Array.Empty<string>());

    public SyntaxHighlightingTests()
    {
        (IConsole console, _) = FakeConsole.CreateStubbedOutput();
        _services = new RoslynServices(
            console,
            new Configuration
            {
                Theme = "Data/theme.json",
            },
            new TestTraceLogger()
        );
    }

    [ Fact ]
    public async Task SyntaxHighlightAsync_GivenCode_DetectsTextSpans()
    {
        IReadOnlyCollection<HighlightedSpan> highlighted
            = await _services.SyntaxHighlightAsync(@"var foo = ""bar"";");
        Assert.Equal(5, highlighted.Count);

        var expected = new TextSpan[]
        {
            new(0, 3), // var
            new(4, 3), // foo
            new(8, 1), // =
            new(10, 5), // "bar"
            new(15, 1), // ;
        };

        Assert.Equal(expected, highlighted.Select(highlight => highlight.TextSpan));
    }

    private readonly RoslynServices _services;
}
