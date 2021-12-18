// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;

namespace CSharpRepl.Services.Roslyn.MetadataResolvers;

using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Scripting;

using PrettyPrompt.Consoles;

using References;

/// <summary>
///     Resolves absolute and relative assembly references. If the assembly has an adjacent
///     assembly.runtimeconfig.json file, the file will be read in order to determine required
///     Shared Frameworks. https://natemcmaster.com/blog/2018/08/29/netcore-primitives-2/
/// </summary>
internal sealed class AssemblyReferenceMetadataResolver : IIndividualMetadataReferenceResolver
{
    public ImmutableArray<PortableExecutableReference> ResolveReference(
        string reference,
        string? baseFilePath,
        MetadataReferenceProperties properties,
        MetadataReferenceResolver compositeResolver
    )
    {
        // resolve relative filepaths
        string fileSystemPath = Path.GetFullPath(reference);

        if (File.Exists(fileSystemPath))
        {
            reference = fileSystemPath;
        }

        ImmutableArray<PortableExecutableReference> references = ScriptMetadataResolver.Default
            .WithSearchPaths(_referenceAssemblyService.ImplementationAssemblyPaths)
            .ResolveReference(reference, baseFilePath, properties);
        LoadSharedFramework(references);

        return references;
    }

    public AssemblyReferenceMetadataResolver(
        IConsole console,
        AssemblyReferenceService referenceAssemblyService
    )
    {
        _console = console;
        _referenceAssemblyService = referenceAssemblyService;
        _loadContext = new AssemblyLoadContext(nameof(CSharpRepl) + "LoadContext");

        AppDomain.CurrentDomain.AssemblyResolve += ResolveByAssemblyName;
    }

    private readonly IConsole _console;
    private readonly AssemblyLoadContext _loadContext;
    private readonly AssemblyReferenceService _referenceAssemblyService;

    private void LoadSharedFramework(ImmutableArray<PortableExecutableReference> references)
    {
        if (references.Length != 1)
        {
            return;
        }

        string? configPath = Path.ChangeExtension(
            references[0]
                .FilePath,
            "runtimeconfig.json"
        );

        if (configPath is null ||
            !File.Exists(configPath))
        {
            return;
        }

        string content = File.ReadAllText(configPath);

        if (JsonSerializer.Deserialize<RuntimeConfigJson>(
                content,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                }
            ) is RuntimeConfigJson config)
        {
            string name = config.RuntimeOptions.Framework.Name;
            Version version
                = SharedFramework.ToDotNetVersion(config.RuntimeOptions.Framework.Version);
            _referenceAssemblyService.LoadSharedFrameworkConfiguration(name, version);
        }
    }

    /// <summary>
    ///     If we're missing an assembly (by exact match), try to find an assembly with the same name but
    ///     different version.
    /// </summary>
    private Assembly? ResolveByAssemblyName(object? sender, ResolveEventArgs args)
    {
        AssemblyName assemblyName = new(args.Name);

        Assembly? located = _referenceAssemblyService.ImplementationAssemblyPaths
            .SelectMany(path => Directory.GetFiles(path, "*.dll"))
            .Where(file => Path.GetFileNameWithoutExtension(file) == assemblyName.Name)
            .Select(_loadContext.LoadFromAssemblyPath)
            .FirstOrDefault();

        if (located?.FullName is not null &&
            (new AssemblyName(located.FullName).Version != assemblyName.Version))
        {
            _console.WriteLine($"Warning: Missing assembly: {args.Name}");
            _console.WriteLine($"            Using instead: {located.FullName}");

            if (args.RequestingAssembly is not null)
            {
                _console.WriteLine(
                    $"             Requested by: {args.RequestingAssembly.FullName}"
                );
            }
        }

        return located;
    }
}

/// <summary>
///     Schema for assembly.runtimeconfig.json files.
/// </summary>
internal sealed class RuntimeConfigJson
{
    public RuntimeOptionsKey RuntimeOptions { get; }

    public RuntimeConfigJson(RuntimeOptionsKey runtimeOptions)
        => RuntimeOptions = runtimeOptions;

    public sealed class FrameworkKey
    {
        public string Name { get; }
        public string Version { get; }

        public FrameworkKey(string name, string version)
        {
            Name = name;
            Version = version;
        }
    }

    public sealed class RuntimeOptionsKey
    {
        public string Tfm { get; }
        public FrameworkKey Framework { get; }

        public RuntimeOptionsKey(string tfm, FrameworkKey framework)
        {
            Tfm = tfm;
            Framework = framework;
        }
    }
}
