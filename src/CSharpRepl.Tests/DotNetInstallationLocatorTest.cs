using System;

namespace CSharpRepl.Tests;

using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;

using Services.Roslyn.References;

using Xunit;

public class DotNetInstallationLocatorTest
{
    [ Fact ]
    public void GetSharedFrameworkConfiguration_Net5GlobalInstallation_IsLocated()
    {
        MockFileSystem fileSystem = new(
            new Dictionary<string, MockFileData>
            {
                {
                    @"/Program Files/dotnet/packs/Microsoft.NETCore.App.Ref/5.0.0/data/FrameworkList.xml",
                    MockFileData.NullObject
                },
                {
                    @"/Program Files/dotnet/packs/Microsoft.NETCore.App.Ref/5.0.0/ref/net5.0/Microsoft.CSharp.dll",
                    MockFileData.NullObject
                },
                {
                    @"/Program Files/dotnet/packs/Microsoft.NETCore.App.Ref/6.0.0-rc.1.21451.13/data/FrameworkList.xml",
                    MockFileData.NullObject
                },
                {
                    @"/Program Files/dotnet/packs/Microsoft.NETCore.App.Ref/6.0.0-rc.1.21451.13/ref/net6.0/Microsoft.CSharp.dll",
                    MockFileData.NullObject
                },
                {
                    @"/Program Files/dotnet/shared/Microsoft.NETCore.App/5.0.10/Microsoft.CSharp.dll",
                    MockFileData.NullObject
                },
                {
                    @"/Program Files/dotnet/shared/Microsoft.NETCore.App/6.0.0-rc.1.21451.13/Microsoft.CSharp.dll",
                    MockFileData.NullObject
                },
            }
        );

        DotNetInstallationLocator locator = new(
            new TestTraceLogger(),
            fileSystem,
            @"/Program Files/dotnet/",
            @"/Users/bob/"
        );

        // system under test
        (string refPath, string implPath) = locator.FindInstallation(
            "Microsoft.NETCore.App",
            new Version(5, 0, 14)
        );

        Assert.Equal(
            DotNetInstallationLocatorTest.CrossPlatform(
                @"/Program Files/dotnet/packs/Microsoft.NETCore.App.Ref/5.0.0/ref/net5.0"
            ),
            DotNetInstallationLocatorTest.CrossPlatform(refPath)
        );
        Assert.Equal(
            DotNetInstallationLocatorTest.CrossPlatform(
                @"/Program Files/dotnet/shared/Microsoft.NETCore.App/5.0.10"
            ),
            DotNetInstallationLocatorTest.CrossPlatform(implPath)
        );
    }

    [ Fact ]
    public void GetSharedFrameworkConfiguration_NoGlobalNet5Assemblies_UsesNuGetInstallation()
    {
        MockFileSystem fileSystem = new(
            new Dictionary<string, MockFileData>
            {
                // no global net5.0 assemblies
                //

                // reference assemblies in .nuget installation
                {
                    @"/Users/bob/.nuget/packages/microsoft.netcore.app.ref/3.1.0/data/FrameworkList.xml",
                    MockFileData.NullObject
                },
                {
                    @"/Users/bob/.nuget/packages/microsoft.netcore.app.ref/3.1.0/ref/netcoreapp3.1/Microsoft.CSharp.dll",
                    MockFileData.NullObject
                },
                {
                    @"/Users/bob/.nuget/packages/microsoft.netcore.app.ref/5.0.0/data/FrameworkList.xml",
                    MockFileData.NullObject
                },
                {
                    @"/Users/bob/.nuget/packages/microsoft.netcore.app.ref/5.0.0/ref/net5.0/Microsoft.CSharp.dll",
                    MockFileData.NullObject
                },

                // implement assemblies in .nuget installation
                {
                    @"/Users/bob/.nuget/packages/microsoft.netcore.app.runtime.win-x64/5.0.8/runtimes/win-x64/lib/net5.0/Microsoft.CSharp.dll",
                    MockFileData.NullObject
                },
                {
                    @"/Users/bob/.nuget/packages/microsoft.netcore.app.runtime.win-x64/3.1.15/runtimes/win-x64/lib/necoreapp3.1/Microsoft.CSharp.dll",
                    MockFileData.NullObject
                },
                {
                    @"/Users/bob/.nuget/packages/microsoft.netcore.app.runtime.win-x86/5.0.8/runtimes/win-x86/lib/net5.0/Microsoft.CSharp.dll",
                    MockFileData.NullObject
                },
                {
                    @"/Users/bob/.nuget/packages/microsoft.netcore.app.runtime.win-arm/5.0.8/runtimes/win-arm/lib/net5.0/Microsoft.CSharp.dll",
                    MockFileData.NullObject
                },
                {
                    @"/Users/bob/.nuget/packages/microsoft.netcore.app.runtime.win-arm64/5.0.8/runtimes/win-arm64/lib/net5.0/Microsoft.CSharp.dll",
                    MockFileData.NullObject
                },
                {
                    @"/Users/bob/.nuget/packages/microsoft.netcore.app.runtime.osx-x64/5.0.8/runtimes/osx-x64/lib/net5.0/Microsoft.CSharp.dll",
                    MockFileData.NullObject
                },
                {
                    @"/Users/bob/.nuget/packages/microsoft.netcore.app.runtime.linux-x64/5.0.8/runtimes/linux-x64/lib/net5.0/Microsoft.CSharp.dll",
                    MockFileData.NullObject
                },
            }
        );

        DotNetInstallationLocator locator = new(
            new TestTraceLogger(),
            fileSystem,
            @"/Program Files/dotnet/",
            @"/Users/bob/"
        );

        // system under test
        (string refPath, string implPath) = locator.FindInstallation(
            "Microsoft.NETCore.App",
            new Version(5, 0, 10)
        );

        Assert.Equal(
            DotNetInstallationLocatorTest.CrossPlatform(
                @"/Users/bob/.nuget/packages/microsoft.netcore.app.ref/5.0.0/ref/net5.0"
            ),
            DotNetInstallationLocatorTest.CrossPlatform(refPath)
        );

        string platform = OperatingSystem.IsWindows()
            ? "win"
            : OperatingSystem.IsLinux()
                ? "linux"
                : OperatingSystem.IsMacOS()
                    ? "osx"
                    : null;
        // it's possible that this could also be the implementation assemblies in ~/.nuget
        Assert.Equal(
            DotNetInstallationLocatorTest.CrossPlatform(
                $@"/Users/bob/.nuget/packages/microsoft.netcore.app.runtime.{platform}-x64/5.0.8/runtimes/{platform}-x64/lib/net5.0"
            ),
            DotNetInstallationLocatorTest.CrossPlatform(implPath)
        );
    }

    [ Fact ]
    public void
        GetSharedFrameworkConfiguration_NoGlobalNet5ReferenceAssemblies_UsesNuGetInstallation()
    {
        MockFileSystem fileSystem = new(
            new Dictionary<string, MockFileData>
            {
                // no net5.0 reference assemblies
                {
                    @"/Program Files/dotnet/packs/Microsoft.NETCore.App.Ref/6.0.0-rc.1.21451.13/data/FrameworkList.xml",
                    MockFileData.NullObject
                },
                {
                    @"/Program Files/dotnet/packs/Microsoft.NETCore.App.Ref/6.0.0-rc.1.21451.13/ref/net6.0/Microsoft.CSharp.dll",
                    MockFileData.NullObject
                },
                {
                    @"/Program Files/dotnet/shared/Microsoft.NETCore.App/5.0.10/Microsoft.CSharp.dll",
                    MockFileData.NullObject
                },
                {
                    @"/Program Files/dotnet/shared/Microsoft.NETCore.App/6.0.0-rc.1.21451.13/Microsoft.CSharp.dll",
                    MockFileData.NullObject
                },

                // reference assemblies in .nuget installation
                {
                    @"/Users/bob/.nuget/packages/microsoft.netcore.app.ref/3.1.0/data/FrameworkList.xml",
                    MockFileData.NullObject
                },
                {
                    @"/Users/bob/.nuget/packages/microsoft.netcore.app.ref/3.1.0/ref/netcoreapp3.1/Microsoft.CSharp.dll",
                    MockFileData.NullObject
                },
                {
                    @"/Users/bob/.nuget/packages/microsoft.netcore.app.ref/5.0.0/data/FrameworkList.xml",
                    MockFileData.NullObject
                },
                {
                    @"/Users/bob/.nuget/packages/microsoft.netcore.app.ref/5.0.0/ref/net5.0/Microsoft.CSharp.dll",
                    MockFileData.NullObject
                },

                // implement assemblies in .nuget installation
                {
                    @"/Users/bob/.nuget/packages/microsoft.netcore.app.runtime.win-x64/5.0.8/runtimes/win-x64/lib/net5.0/Microsoft.CSharp.dll",
                    MockFileData.NullObject
                },
                {
                    @"/Users/bob/.nuget/packages/microsoft.netcore.app.runtime.win-x64/3.1.15/runtimes/win-x64/lib/necoreapp3.1/Microsoft.CSharp.dll",
                    MockFileData.NullObject
                },
                {
                    @"/Users/bob/.nuget/packages/microsoft.netcore.app.runtime.win-x86/5.0.8/runtimes/win-x86/lib/net5.0/Microsoft.CSharp.dll",
                    MockFileData.NullObject
                },
                {
                    @"/Users/bob/.nuget/packages/microsoft.netcore.app.runtime.win-arm/5.0.8/runtimes/win-arm/lib/net5.0/Microsoft.CSharp.dll",
                    MockFileData.NullObject
                },
                {
                    @"/Users/bob/.nuget/packages/microsoft.netcore.app.runtime.win-arm64/5.0.8/runtimes/win-arm64/lib/net5.0/Microsoft.CSharp.dll",
                    MockFileData.NullObject
                },
                {
                    @"/Users/bob/.nuget/packages/microsoft.netcore.app.runtime.osx-x64/5.0.8/runtimes/osx-x64/lib/net5.0/Microsoft.CSharp.dll",
                    MockFileData.NullObject
                },
                {
                    @"/Users/bob/.nuget/packages/microsoft.netcore.app.runtime.linux-x64/5.0.8/runtimes/linux-x64/lib/net5.0/Microsoft.CSharp.dll",
                    MockFileData.NullObject
                },
            }
        );

        DotNetInstallationLocator locator = new(
            new TestTraceLogger(),
            fileSystem,
            @"/Program Files/dotnet/",
            @"/Users/bob/"
        );

        // system under test
        (string refPath, string implPath) = locator.FindInstallation(
            "Microsoft.NETCore.App",
            new Version(5, 0, 10)
        );

        Assert.Equal(
            DotNetInstallationLocatorTest.CrossPlatform(
                "/Users/bob/.nuget/packages/microsoft.netcore.app.ref/5.0.0/ref/net5.0"
            ),
            DotNetInstallationLocatorTest.CrossPlatform(refPath)
        );
        // it's possible that this could also be the implementation assemblies in ~/.nuget
        Assert.Equal(
            DotNetInstallationLocatorTest.CrossPlatform(
                @"/Program Files/dotnet/shared/Microsoft.NETCore.App/5.0.10"
            ),
            DotNetInstallationLocatorTest.CrossPlatform(implPath)
        );
    }

    /// <summary>
    ///     Converts a path to the operating system path. e.g. "/" vs "\", remove drive letters ("C:"),
    ///     etc.
    ///     This is needed so the tests assert correctly when they're running under Windows, Linux, and Mac
    ///     OS.
    /// </summary>
    private static string CrossPlatform(string path)
    {
        string fullPath = Path.GetFullPath(path);

        return OperatingSystem.IsWindows()
            ? fullPath.Split(Path.VolumeSeparatorChar, 2)[1]
            : fullPath;
    }
}
