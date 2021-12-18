using System;

namespace CSharpRepl.Tests;

using System.Collections.Generic;
using System.IO;

using Logging;

using Services.Logging;

using Xunit;

/// <summary>
///     more of an integration test than anything; we want to make sure the logger actually does log to
///     a file.
/// </summary>
public class TraceLoggerTests
{
    [ Fact ]
    public void Create_ThenLog_WritesToFile()
    {
        string path = Path.GetTempFileName();
        ITraceLogger logger = TraceLogger.Create(path);

        logger.Log("Hello World, I'm a hopeful and optimistic REPL.");
        logger.Log(() => "Arrgghh an error");

        string[] loggedLines = File.ReadAllLines(path);
        Assert.Contains("Trace session starting", loggedLines[0]);
        Assert.Contains("Hello World, I'm a hopeful and optimistic REPL.", loggedLines[1]);
        Assert.Contains("Arrgghh an error", loggedLines[2]);
    }

    [ Fact ]
    public void LogPaths_GivenPaths_GroupsByPrefix()
    {
        string path = Path.GetTempFileName();
        ITraceLogger logger = TraceLogger.Create(path);

        logger.LogPaths(
            "Some Files",
            () => new[]
            {
                @"/Foo/Bar.txt", @"/Foo/Baz.txt",
            }
        );

        string[] loggedLines = File.ReadAllLines(path);
        Assert.Contains("Trace session starting", loggedLines[0]);
        Assert.Contains(@"Some Files: ", loggedLines[1]);
        Assert.EndsWith(@"[""Bar.txt"", ""Baz.txt""]", loggedLines[1]);
    }
}

/// <summary>
///     Executes the delayed evaluation Funcs for testing purposes (to make sure they don't throw
///     exceptions).
/// </summary>
public class TestTraceLogger : ITraceLogger
{
    public void Log(string message)
    {
    }

    public void Log(Func<string> message)
    {
        message();
    }

    public void LogPaths(string message, Func<IEnumerable<string>> paths)
    {
        paths();
    }
}
