// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;

namespace CSharpRepl.Logging;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Services.Logging;

/// <summary>
///     TraceLogger is used when the --trace flag is provided, and logs debugging details to a file.
/// </summary>
internal sealed class TraceLogger : ITraceLogger
{
    public void Log(string message)
    {
        File.AppendAllText(_path, $"{DateTime.UtcNow:s} - {message}{Environment.NewLine}");
    }

    public void Log(Func<string> message)
    {
        Log(message());
    }

    public void LogPaths(string message, Func<IEnumerable<string?>> paths)
    {
        Log(message + ": " + TraceLogger.GroupPathsByPrefixForLogging(paths()));
    }

    public static ITraceLogger Create(string path)
    {
        string tracePath = Path.GetFullPath(path);
        TraceLogger logger = new(tracePath);

        // let the user know where the trace is being logged to, by writing to the REPL.
        Console.Write(Environment.NewLine + "Writing trace log to ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(tracePath + Environment.NewLine);
        Console.ResetColor();

        AppDomain.CurrentDomain.UnhandledException += (_, evt)
            => logger.Log("Unhandled Exception: " + evt.ExceptionObject);
        TaskScheduler.UnobservedTaskException += (_, evt)
            => logger.Log("Unoberved Task Exception: " + evt.Exception);

        logger.Log("Trace session starting");

        return logger;
    }

    private TraceLogger(string path)
        => _path = path;

    private readonly string _path;

    private static string GroupPathsByPrefixForLogging(IEnumerable<string?> paths)
    {
        return string.Join(
            ", ",
            paths.GroupBy(Path.GetDirectoryName)
                .Select(
                    group
                        => $@"""{group.Key}"": [{string.Join(", ", group.Select(path => $@"""{Path.GetFileName(path)}"""))}]"
                )
        );
    }
}
