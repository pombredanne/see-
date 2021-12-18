// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Nuke.Common.Tooling;

namespace CSharpRepl._build;

[ TypeConverter(typeof(TypeConverter<Configuration>)) ]
public class Configuration : Enumeration
{
    private protected static Configuration _debug = new()
    {
        Value = nameof(Configuration.Debug),
    };

    public static implicit operator string([ NotNull ] Configuration configuration)
        => configuration.Value;

    private protected static Configuration _release = new()
    {
        Value = nameof(Configuration.Release),
    };

    public static Configuration Release
    {
        get => Configuration._release;
        set => Configuration._release = value;
    }

    public static Configuration Debug
    {
        get => Configuration._debug;
        set => Configuration._debug = value;
    }
}