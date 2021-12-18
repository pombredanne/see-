using System;

namespace CSharpRepl.Tests;

using System.Threading.Tasks;

using PrettyPrompt.Consoles;

using Services;
using Services.Roslyn;

using Xunit;

[ Collection(nameof(RoslynServices)) ]
public class CompleteStatementTests : IAsyncLifetime
{
    public Task DisposeAsync()
        => Task.CompletedTask;

    public Task InitializeAsync()
        => _services.WarmUpAsync(Array.Empty<string>());

    public CompleteStatementTests()
    {
        (IConsole console, _) = FakeConsole.CreateStubbedOutput();
        _services = new RoslynServices(console, new Configuration(), new TestTraceLogger());
    }

    [ Theory, InlineData("var x = 5;", true), InlineData("var x = ", false),
      InlineData("if (x == 4)", false), InlineData("if (x == 4) return;", true),
      InlineData("if you're happy and you know it, syntax error!", false), ]
    public async Task IsCompleteStatement(string code, bool shouldBeCompleteStatement)
    {
        bool isCompleteStatement = await _services.IsTextCompleteStatementAsync(code);
        Assert.Equal(shouldBeCompleteStatement, isCompleteStatement);
    }

    private readonly RoslynServices _services;
}
