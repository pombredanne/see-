// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;

namespace CSharpRepl.Tests;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using PrettyPrompt.Consoles;

using Services;
using Services.Completion;
using Services.Roslyn;
using Services.Roslyn.Scripting;

using Xunit;

[ Collection(nameof(RoslynServices)) ]
public class EvaluationTests : IAsyncLifetime
{
    public Task DisposeAsync()
        => Task.CompletedTask;

    public Task InitializeAsync()
        => _services.WarmUpAsync(Array.Empty<string>());

    public EvaluationTests()
    {
        (IConsole console, StringBuilder stdout) = FakeConsole.CreateStubbedOutput();
        _services = new RoslynServices(console, new Configuration(), new TestTraceLogger());
        _stdout = stdout;
    }

    [ Fact ]
    public async Task Evaluate_AbsoluteAssemblyReference_CanReferenceAssembly()
    {
        string absolutePath = Path.GetFullPath("./Data/DemoLibrary.dll");
        EvaluationResult referenceResult = await _services.EvaluateAsync(@$"#r ""{absolutePath}""");
        EvaluationResult importResult = await _services.EvaluateAsync("using DemoLibrary;");
        EvaluationResult multiplyResult = await _services.EvaluateAsync("DemoClass.Multiply(7, 6)");

        Assert.IsType<EvaluationResult.Success>(referenceResult);
        Assert.IsType<EvaluationResult.Success>(importResult);
        EvaluationResult.Success successfulResult
            = Assert.IsType<EvaluationResult.Success>(multiplyResult);
        Assert.Equal(42, successfulResult.ReturnValue);
    }

    [ Fact ]
    public async Task Evaluate_AssemblyReferenceInSearchPath_CanReferenceAssembly()
    {
        EvaluationResult referenceResult = await _services.EvaluateAsync(@"#r ""System.Linq.dll""");

        Assert.IsType<EvaluationResult.Success>(referenceResult);
    }

    [ Fact ]
    public async Task Evaluate_AssemblyReferenceWithSharedFramework_ReferencesSharedFramework()
    {
        EvaluationResult referenceResult
            = await _services.EvaluateAsync(@"#r ""./Data/WebApplication1.dll""");
        EvaluationResult sharedFrameworkResult
            = await _services.EvaluateAsync(@"using Microsoft.AspNetCore.Hosting;");
        EvaluationResult applicationResult
            = await _services.EvaluateAsync(@"using WebApplication1;");

        Assert.IsType<EvaluationResult.Success>(referenceResult);
        Assert.IsType<EvaluationResult.Success>(sharedFrameworkResult);
        Assert.IsType<EvaluationResult.Success>(applicationResult);

        IReadOnlyCollection<CompletionItemWithDescription> completions
            = await _services.CompleteAsync(@"using WebApplicat", 17);
        Assert.Contains(
            "WebApplication1",
            completions.Select(c => c.Item.DisplayText)
                .First(text => text.StartsWith("WebApplicat"))
        );
    }

    [ Fact ]
    public async Task Evaluate_LiteralInteger_ReturnsInteger()
    {
        EvaluationResult result = await _services.EvaluateAsync("5");

        EvaluationResult.Success success = Assert.IsType<EvaluationResult.Success>(result);
        Assert.Equal("5", success.Input);

        var returnValue = Assert.IsType<int>(success.ReturnValue);
        Assert.Equal(5, returnValue);
    }

    [ Fact ]
    public async Task Evaluate_NugetPackage_InstallsPackage()
    {
        EvaluationResult installation
            = await _services.EvaluateAsync(@"#r ""nuget:Newtonsoft.Json""");
        EvaluationResult usage = await _services.EvaluateAsync(
            @"Newtonsoft.Json.JsonConvert.SerializeObject(new { Foo = ""bar"" })"
        );

        EvaluationResult.Success installationResult
            = Assert.IsType<EvaluationResult.Success>(installation);
        EvaluationResult.Success usageResult = Assert.IsType<EvaluationResult.Success>(usage);

        Assert.Null(installationResult.ReturnValue);
        Assert.Contains(
            installationResult.References,
            r => r.Display.EndsWith("Newtonsoft.Json.dll")
        );
        Assert.Contains("Adding references for Newtonsoft.Json", _stdout.ToString());
        Assert.Equal(@"{""Foo"":""bar""}", usageResult.ReturnValue);
    }

    [ Fact ]
    public async Task Evaluate_NugetPackageVersioned_InstallsPackageVersion()
    {
        EvaluationResult installation
            = await _services.EvaluateAsync(@"#r ""nuget:Microsoft.CodeAnalysis.CSharp, 3.11.0""");
        EvaluationResult usage = await _services.EvaluateAsync(
            @"Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(""5"")"
        );

        EvaluationResult.Success installationResult
            = Assert.IsType<EvaluationResult.Success>(installation);
        EvaluationResult.Success usageResult = Assert.IsType<EvaluationResult.Success>(usage);

        Assert.Null(installationResult.ReturnValue);
        Assert.NotNull(usageResult.ReturnValue);
        Assert.Contains(
            "Adding references for Microsoft.CodeAnalysis.CSharp.3.11.0",
            _stdout.ToString()
        );
    }

    [ Fact ]
    public async Task Evaluate_ProjectReference_ReferencesProject()
    {
        EvaluationResult referenceResult = await _services.EvaluateAsync(
            @"#r ""./../../../../CSharpRepl.Services/CSharpRepl.Services.csproj"""
        );
        EvaluationResult importResult
            = await _services.EvaluateAsync(@"using CSharpRepl.Services;");

        Assert.IsType<EvaluationResult.Success>(referenceResult);
        Assert.IsType<EvaluationResult.Success>(importResult);
    }

    [ Fact ]
    public async Task Evaluate_RelativeAssemblyReference_CanReferenceAssembly()
    {
        EvaluationResult referenceResult
            = await _services.EvaluateAsync(@"#r ""./Data/DemoLibrary.dll""");
        EvaluationResult importResult = await _services.EvaluateAsync("using DemoLibrary;");
        EvaluationResult multiplyResult = await _services.EvaluateAsync("DemoClass.Multiply(5, 6)");

        Assert.IsType<EvaluationResult.Success>(referenceResult);
        Assert.IsType<EvaluationResult.Success>(importResult);
        EvaluationResult.Success successfulResult
            = Assert.IsType<EvaluationResult.Success>(multiplyResult);
        Assert.Equal(30, successfulResult.ReturnValue);
    }

    [ Fact ]
    public async Task Evaluate_Variable_ReturnsValue()
    {
        EvaluationResult variableAssignment
            = await _services.EvaluateAsync(@"var x = ""Hello World"";");
        EvaluationResult variableUsage
            = await _services.EvaluateAsync(@"x.Replace(""World"", ""Mundo"")");

        EvaluationResult.Success assignment
            = Assert.IsType<EvaluationResult.Success>(variableAssignment);
        EvaluationResult.Success usage = Assert.IsType<EvaluationResult.Success>(variableUsage);
        Assert.Null(assignment.ReturnValue);
        Assert.Equal("Hello Mundo", usage.ReturnValue);
    }

    private readonly RoslynServices _services;
    private readonly StringBuilder _stdout;
}
