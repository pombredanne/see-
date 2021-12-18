using System;

namespace CSharpRepl.Tests;

using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using PrettyPrompt;
using PrettyPrompt.Consoles;

using PrettyPromptConfig;

using Services;
using Services.Roslyn;

using Xunit;

[ Collection(nameof(RoslynServices)) ]
public class PromptConfigurationTests : IAsyncLifetime
{
    public Task DisposeAsync()
        => Task.CompletedTask;

    public Task InitializeAsync()
        => _services.WarmUpAsync(Array.Empty<string>());

    public PromptConfigurationTests()
    {
        (IConsole console, StringBuilder stdout) = FakeConsole.CreateStubbedOutput();
        _console = console;
        _stdout = stdout;

        _services = new RoslynServices(console, new Configuration(), new TestTraceLogger());
    }

    public static IEnumerable<object[]> KeyPresses()
    {
        yield return new object[]
        {
            ConsoleKey.F1,
        };
        yield return new object[]
        {
            (ConsoleModifiers.Control, ConsoleKey.F1),
        };
        yield return new object[]
        {
            ConsoleKey.F9,
        };
        yield return new object[]
        {
            (ConsoleModifiers.Control, ConsoleKey.F9),
        };
        yield return new object[]
        {
            ConsoleKey.F12,
        };
        yield return new object[]
        {
            (ConsoleModifiers.Control, ConsoleKey.D),
        };
    }

    [ Theory, MemberData(nameof(PromptConfigurationTests.KeyPresses)), ]
    public void PromptConfiguration_CanCreate(object keyPress)
    {
        PromptCallbacks configuration = PromptConfiguration.Configure(_console, _services);
        configuration.KeyPressCallbacks[keyPress]
            .Invoke("Console.WriteLine(\"Hi!\");", 0);
    }

    private readonly IConsole _console;
    private readonly RoslynServices _services;
    private readonly StringBuilder _stdout;
}
