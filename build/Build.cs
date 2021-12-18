// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

// ReSharper disable UnusedMember.Local

namespace CSharpRepl._build;

[ CheckBuildProjectConfigurations, ShutdownDotNetAfterServerBuild, ]
public class Build : NukeBuild
{
    private static AbsolutePath SourceDirectory => NukeBuild.RootDirectory / "src";

    private static AbsolutePath ArtifactsDirectory => NukeBuild.RootDirectory / "artifacts";

    [ NotNull ]
    private Target Clean => _ => _
        .Before(Restore)
        .Executes(static () =>
            {
                Build.SourceDirectory.GlobDirectories("**/bin", "**/obj")
                    .ForEach(FileSystemTasks.DeleteDirectory);
                FileSystemTasks.EnsureCleanDirectory(Build.ArtifactsDirectory);
            }
        );

    [ NotNull ]
    private Target Restore => _ => _
        .Executes(() =>
            {
                DotNetTasks.DotNetRestore(s => s
                    .SetProjectFile(Solution)
                );
            }
        );

    [NotNull]
    private Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
            {
                DotNetBuildSettings Configurator(DotNetBuildSettings s)
                {
                    if (Version is not null)
                    {
                        return s.SetProjectFile(Solution)
                            .SetConfiguration(Configuration)
                            .SetAssemblyVersion(Version.AssemblySemVer)
                            .SetFileVersion(Version.AssemblySemFileVer)
                            .SetInformationalVersion(Version.InformationalVersion)
                            .EnableNoRestore();
                    }

                    return s.SetProjectFile(Solution)
                        .SetConfiguration(Configuration)
                        .EnableNoRestore();
                }

                DotNetTasks.DotNetBuild(Configurator);
            }
        );

    [NotNull]
    private Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() =>
            {
                DotNetTestSettings Configurator(DotNetTestSettings s)
                {
                    return s.SetProjectFile(Solution)
                        .SetConfiguration(Configuration)
                        // .EnableNoBuild()
                        .EnableNoRestore()
                        .SetVerbosity(DotNetVerbosity.Normal);
                }

                DotNetTasks.DotNetTest(Configurator);
            }
        );

    /// <summary>
    ///     Support plugins are available for:
    ///     - JetBrains ReSharper        https://nuke.build/resharper
    ///     - JetBrains Rider            https://nuke.build/rider
    ///     - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///     - Microsoft VSCode           https://nuke.build/vscode
    /// </summary>
    public static int Main()
        => NukeBuild.Execute<Build>(static x => x.Compile);

    [ Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)") ]
    private readonly Configuration Configuration = NukeBuild.IsLocalBuild
        ? Configuration.Debug
        : Configuration.Release;

    [ CanBeNull, ] private protected GitRepository _gitRepository;
    [ CanBeNull, ] private protected GitVersion _gitVersion;
    [ CanBeNull, ] private protected Solution _solution;

    [ GitRepository, CanBeNull ]
    // ReSharper disable once UnusedMember.Global
    public GitRepository GitRepository
    {
        get => _gitRepository;
        set => _gitRepository = value;
    }

    [ GitVersion, CanBeNull ]
    public GitVersion Version
    {
        get => _gitVersion;
        set => _gitVersion = value;
    }

    [ Solution, CanBeNull ]
    public Solution Solution
    {
        get => _solution;
        set => _solution = value;
    }
}