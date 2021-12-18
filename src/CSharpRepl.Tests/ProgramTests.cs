using System;

namespace CSharpRepl.Tests;

using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

public class ProgramTests
{
    [ Fact ]
    public async Task MainMethod_CannotParse_DoesNotThrow()
    {
        using OutputCollector outputCollector
            = OutputCollector.Capture(out _, out StringWriter capturedError);

        await Program.Main(
            new[]
            {
                "bonk",
            }
        );

        var error = capturedError.ToString();
        Assert.Equal("Unrecognized command or argument 'bonk'" + Environment.NewLine, error);
    }

    [ Fact ]
    public async Task MainMethod_Help_ShowsHelp()
    {
        using OutputCollector outputCollector
            = OutputCollector.Capture(out StringWriter capturedOutput);
        await Program.Main(
            new[]
            {
                "-h",
            }
        );
        var output = capturedOutput.ToString();

        Assert.Contains(
            "Starts a REPL (read eval print loop) according to the provided [OPTIONS].",
            output
        );
        // should show default shared framework
        Assert.Contains("Microsoft.NETCore.App (default)", output);
    }

    [ Fact ]
    public async Task MainMethod_Version_ShowsVersion()
    {
        using OutputCollector outputCollector
            = OutputCollector.Capture(out StringWriter capturedOutput);

        await Program.Main(
            new[]
            {
                "-v",
            }
        );

        var output = capturedOutput.ToString();
        Assert.Contains("C# REPL", output);
        Version version = new(output.Trim("C# REPL-rc-alpha-beta\r\n".ToCharArray()));
        Assert.True(version.Major + version.Minor > 0);
    }
}

/// <summary>
///     Captures standard output. Because there's only one Console.Out,
///     this forces single threaded execution of unit tests that use it.
/// </summary>
public sealed class OutputCollector : IDisposable
{
    public void Dispose()
    {
        Console.SetOut(_normalStandardOutput);
        Console.SetOut(_normalStandardError);
        OutputCollector.Semaphore.Release();
    }

    public static OutputCollector Capture(out StringWriter capturedOutput)
    {
        OutputCollector.Semaphore.WaitOne();

        OutputCollector outputCollector = new();
        capturedOutput = outputCollector._fakeConsoleOutput;

        return outputCollector;
    }

    public static OutputCollector Capture(
        out StringWriter capturedOutput,
        out StringWriter capturedError
    )
    {
        OutputCollector.Semaphore.WaitOne();

        OutputCollector outputCollector = new();
        capturedOutput = outputCollector._fakeConsoleOutput;
        capturedError = outputCollector._fakeConsoleError;

        return outputCollector;
    }

    private OutputCollector()
    {
        _normalStandardOutput = Console.Out;
        _normalStandardError = Console.Error;
        _fakeConsoleOutput = new StringWriter();
        _fakeConsoleError = new StringWriter();
        Console.SetOut(_fakeConsoleOutput);
        Console.SetError(_fakeConsoleError);
    }

    private readonly StringWriter _fakeConsoleError;
    private readonly StringWriter _fakeConsoleOutput;
    private readonly TextWriter _normalStandardError;
    private readonly TextWriter _normalStandardOutput;
    private static readonly Semaphore Semaphore = new(1, 1);
}
