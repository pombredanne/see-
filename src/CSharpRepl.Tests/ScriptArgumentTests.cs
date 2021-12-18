using System;

namespace CSharpRepl.Tests;

using System.Text;
using System.Threading.Tasks;

using PrettyPrompt.Consoles;

using Services;
using Services.Roslyn;
using Services.Roslyn.Scripting;

using Xunit;

[ Collection(nameof(RoslynServices)) ]
public class ScriptArgumentTests
{
    [ Fact ]
    public async Task Evaluate_PrettyPrint_PrintsPrettily()
    {
        (IConsole console, StringBuilder stdOut) = FakeConsole.CreateStubbedOutput();
        RoslynServices services = new(console, new Configuration(), new TestTraceLogger());

        await services.WarmUpAsync(Array.Empty<string>());
        _ = await services.EvaluateAsync("using System.Globalization;");
        _ = await services.EvaluateAsync(
            "CultureInfo.DefaultThreadCurrentCulture = new System.Globalization.CultureInfo(\"en-US\");"
        );
        EvaluationResult printStatement = await services.EvaluateAsync("Print(DateTime.MinValue)");

        Assert.IsType<EvaluationResult.Success>(printStatement);
        Assert.Equal("[1/1/0001 12:00:00 AM]" + Environment.NewLine, stdOut.ToString());
    }

    [ Theory, InlineData("args[0]"), InlineData("Args[0]"), ]
    // array accessor
    // IList<string> accessor
    public async Task Evaluate_WithArguments_ArgumentsAvailable(string argsAccessor)
    {
        (IConsole console, _) = FakeConsole.CreateStubbedOutput();
        RoslynServices services = new(console, new Configuration(), new TestTraceLogger());
        var args = new[]
        {
            "Howdy",
        };

        await services.WarmUpAsync(args);
        EvaluationResult variableAssignment
            = await services.EvaluateAsync($@"var x = {argsAccessor};");
        EvaluationResult variableUsage = await services.EvaluateAsync(@"x");

        Assert.IsType<EvaluationResult.Success>(variableAssignment);
        EvaluationResult.Success usage = Assert.IsType<EvaluationResult.Success>(variableUsage);
        Assert.Equal("Howdy", usage.ReturnValue);
    }
}
