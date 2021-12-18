// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;

namespace CSharpRepl.Tests;

using System.Threading.Tasks;

using PrettyPrompt.Consoles;

using Services;
using Services.Roslyn;
using Services.SymbolExploration;

using Xunit;

[ Collection(nameof(RoslynServices)) ]
public class SymbolExplorerTests : IAsyncLifetime
{
    public Task DisposeAsync()
        => Task.CompletedTask;

    public Task InitializeAsync()
        => _services.WarmUpAsync(Array.Empty<string>());

    public SymbolExplorerTests()
    {
        (IConsole console, _) = FakeConsole.CreateStubbedOutput();
        _services = new RoslynServices(console, new Configuration(), new TestTraceLogger());
    }

    [ Fact ]
    public async Task GetSymbolAtIndex_ClassInSourceLinkedAssembly_ReturnsSourceLinkUrl()
    {
        // should return a string like https://www.github.com/dotnet/runtime/blob/208e377a5329ad6eb1db5e5fb9d4590fa50beadd/src/libraries/System.Console/src/System/Console.cs
        SymbolResult symbol = await _services.GetSymbolAtIndexAsync(
            @"Console.WriteLine(""howdy"")",
            "Conso".Length
        );

        Assert.StartsWith("https://www.github.com/dotnet/runtime/", symbol.Url);
        Assert.EndsWith("Console.cs", symbol.Url);
    }

    [ Fact ]
    public async Task GetSymbolAtIndex_EventInSourceLinkedAssembly_ReturnsSourceLinkUrl()
    {
        // should return a string like https://www.github.com/dotnet/runtime/blob/208e377a5329ad6eb1db5e5fb9d4590fa50beadd/src/libraries/System.Console/src/System/Console.cs
        SymbolResult symbol = await _services.GetSymbolAtIndexAsync(
            @"Console.CancelKeyPress",
            "Console.CancelKe".Length
        );

        SymbolExplorerTests.AssertLinkWithLineNumber(symbol);
    }

    [ Fact ]
    public async Task GetSymbolAtIndex_InvalidSymbol_NoException()
    {
        SymbolResult symbol = await _services.GetSymbolAtIndexAsync(@"wow!", 2);

        Assert.Equal(SymbolResult.Unknown, symbol);
    }

    [ Fact ]
    public async Task GetSymbolAtIndex_MethodInSourceLinkedAssembly_ReturnsSourceLinkUrl()
    {
        // should return a string like https://www.github.com/dotnet/runtime/blob/208e377a5329ad6eb1db5e5fb9d4590fa50beadd/src/libraries/System.Console/src/System/Console.cs#L635-L636
        SymbolResult symbol = await _services.GetSymbolAtIndexAsync(
            @"Console.WriteLine(""howdy"")",
            "Console.Wri".Length
        );

        SymbolExplorerTests.AssertLinkWithLineNumber(symbol);
    }

    [ Fact ]
    public async Task GetSymbolAtIndex_NonSourceLinkedAssembly_NoException()
    {
        _ = await _services.EvaluateAsync(@"#r ""./Data/DemoLibrary.dll""");
        _ = await _services.EvaluateAsync("using DemoLibrary;");
        SymbolResult symbol = await _services.GetSymbolAtIndexAsync(
            "DemoClass.Multiply",
            "DemoClass.Multi".Length
        );

        Assert.Equal("DemoLibrary.DemoClass.Multiply", symbol.SymbolDisplay);
        Assert.Null(symbol.Url);
    }

    [ Fact ]
    public async Task GetSymbolAtIndex_PropertyInSourceLinkedAssembly_ReturnsSourceLinkUrl()
    {
        // should return a string like https://www.github.com/dotnet/runtime/blob/208e377a5329ad6eb1db5e5fb9d4590fa50beadd/src/libraries/System.Console/src/System/Console.cs
        SymbolResult symbol
            = await _services.GetSymbolAtIndexAsync(@"Console.Out", "Console.Ou".Length);

        SymbolExplorerTests.AssertLinkWithLineNumber(symbol);
    }

    [ Fact ]
    public async Task GetSymbolAtIndex_ReturnsFullyQualifiedName()
    {
        SymbolResult symbol = await _services.GetSymbolAtIndexAsync(
            @"Console.WriteLine(""howdy"")",
            "Console.Wri".Length
        );
        Assert.Equal("System.Console.WriteLine", symbol.SymbolDisplay);
    }

    private readonly RoslynServices _services;

    private static void AssertLinkWithLineNumber(SymbolResult symbol)
    {
        string[] urlParts = symbol.Url.Split('#');
        Assert.Equal(2, urlParts.Length);

        string url = urlParts[0];
        Assert.StartsWith("https://www.github.com/dotnet/runtime/", url);

        string lineHash = urlParts[1];
        const string LINE_PATTERN = "L[0-9]+";
        Assert.Matches($"^{LINE_PATTERN}-{LINE_PATTERN}$", lineHash);
    }
}
