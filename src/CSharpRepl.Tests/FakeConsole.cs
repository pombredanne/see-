// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace CSharpRepl.Tests;

using System.Text;

using NSubstitute;

using PrettyPrompt.Consoles;

internal static class FakeConsole
{
    public static (IConsole console, StringBuilder stdout) CreateStubbedOutput()
    {
        IConsole stub = Substitute.For<IConsole>();
        StringBuilder stdout = new();
        stub.When(c => c.Write(Arg.Any<string>()))
            .Do(args => stdout.Append(args.Arg<string>()));
        stub.When(c => c.WriteLine(Arg.Any<string>()))
            .Do(args => stdout.AppendLine(args.Arg<string>()));

        return (stub, stdout);
    }

    public static (IConsole console, StringBuilder stdout, StringBuilder stderr)
        CreateStubbedOutputAndError()
    {
        IConsole stub = Substitute.For<IConsole>();
        StringBuilder stdout = new();
        StringBuilder stderr = new();
        stub.When(c => c.Write(Arg.Any<string>()))
            .Do(args => stdout.Append(args.Arg<string>()));
        stub.When(c => c.WriteLine(Arg.Any<string>()))
            .Do(args => stdout.AppendLine(args.Arg<string>()));
        stub.When(c => c.WriteError(Arg.Any<string>()))
            .Do(args => stderr.Append(args.Arg<string>()));
        stub.When(c => c.WriteErrorLine(Arg.Any<string>()))
            .Do(args => stderr.AppendLine(args.Arg<string>()));

        return (stub, stdout, stderr);
    }
}
