using System;

namespace CSharpRepl.Tests;

using System.Threading.Tasks;

using NSubstitute;

using PrettyPrompt;
using PrettyPrompt.Consoles;

using Services;
using Services.Roslyn;

using Xunit;

public sealed class RoslynServicesFixture : IAsyncLifetime
{
    public IConsole ConsoleStub { get; }
    public IPrompt PromptStub { get; }
    public RoslynServices RoslynServices { get; }

    public Task DisposeAsync()
        => Task.CompletedTask;

    public Task InitializeAsync()
        => RoslynServices.WarmUpAsync(Array.Empty<string>());

    public RoslynServicesFixture()
    {
        ConsoleStub = Substitute.For<IConsole>();
        PromptStub = Substitute.For<IPrompt>();
        RoslynServices = new RoslynServices(
            ConsoleStub,
            new Configuration(),
            new TestTraceLogger()
        );
    }
}
