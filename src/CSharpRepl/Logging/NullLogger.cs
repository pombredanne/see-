// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;

namespace CSharpRepl.Logging;

using System.Collections.Generic;

using Services.Logging;

/// <summary>
///     NullLogger is used by default. <see cref="TraceLogger" /> is used when the --trace flag is
///     provided.
/// </summary>
internal sealed class NullLogger : ITraceLogger
{
    public void Log(string message)
    {
        /* null logger does not log */
    }

    public void Log(Func<string> message)
    {
        /* null logger does not log */
    }

    public void LogPaths(string message, Func<IEnumerable<string?>> paths)
    {
        /* null logger does not log */
    }
}
