// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;

namespace CSharpRepl.Services.Roslyn.References;

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using Extensions;

using Logging;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
///     Manages references to assemblies. It tracks "reference assemblies" and "implementation
///     assemblies" separately,
///     because the Script APIs require implementation assemblies and the workspace APIs require
///     reference assemblies.
///     This service is stateful, as assemblies and shared frameworks can be added dynamically in the
///     REPL.
/// </summary>
/// <remarks>
///     Useful notes
///     https://github.com/dotnet/roslyn/blob/main/docs/wiki/Runtime-code-generation-using-Roslyn-compilations-in-.NET-Core-App.md
/// </remarks>
internal sealed class AssemblyReferenceService
{
    public IReadOnlySet<string> ImplementationAssemblyPaths => _implementationAssemblyPaths;

    public IReadOnlySet<MetadataReference> LoadedImplementationAssemblies => _loadedImplementationAssemblies;

    public IReadOnlySet<MetadataReference> LoadedReferenceAssemblies => _loadedReferenceAssemblies;

    public IReadOnlyCollection<UsingDirectiveSyntax> Usings => _usings;

    public AssemblyReferenceService(Configuration config, ITraceLogger logger)
    {
        _dotnetInstallationLocator = new DotNetInstallationLocator(logger);
        _referenceAssemblyPaths = new HashSet<string>();
        _implementationAssemblyPaths = new HashSet<string>();
        _sharedFrameworkImplementationAssemblyPaths = new HashSet<string>();
        _cachedMetadataReferences = new ConcurrentDictionary<string, MetadataReference>();
        _loadedReferenceAssemblies
            = new HashSet<MetadataReference>(new AssemblyReferenceComparer());
        _loadedImplementationAssemblies
            = new HashSet<MetadataReference>(new AssemblyReferenceComparer());

        _usings = new[]
            {
                "System",
                "System.IO",
                "System.Collections.Generic",
                "System.Linq",
                "System.Net.Http",
                "System.Text",
                "System.Threading.Tasks",
            }.Concat(config.Usings)
            .Select(name => SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(name)))
            .ToHashSet();

        (string framework, Version version)
            = AssemblyReferenceService.GetDesiredFrameworkVersion(config.Framework);
        SharedFramework[] sharedFrameworks = GetSharedFrameworkConfiguration(framework, version);
        LoadSharedFrameworkConfiguration(sharedFrameworks);

        logger.Log(() => $".NET Version: {framework} / {version}");
        logger.Log(() => $"Reference Assembly Paths: {string.Join(", ", _referenceAssemblyPaths)}");
        logger.Log(
            ()
                => $"Implementation Assembly Paths: {string.Join(", ", _implementationAssemblyPaths)}"
        );
        logger.Log(
            ()
                => $"Shared Framework Paths: {string.Join(", ", _sharedFrameworkImplementationAssemblyPaths)}"
        );
        logger.LogPaths(
            "Loaded Reference Assemblies",
            () => _loadedReferenceAssemblies.Select(a => a.Display)
        );
        logger.LogPaths(
            "Loaded Implementation Assemblies",
            () => _loadedImplementationAssemblies.Select(a => a.Display)
        );
    }

    /// <summary>
    ///     Returns a SharedFramework that contains a list of reference and implementation assemblies in
    ///     that framework.
    ///     It first tries to use a globally installed shared framework (e.g. in C:\Program Files\dotnet\)
    ///     and falls back
    ///     to a shared framework installed in C:\Users\username\.nuget\packages\.
    ///     Microsoft.NETCore.App is always returned, in addition to any other specified framework.
    /// </summary>
    /// <exception cref="InvalidOperationException">If no shared framework could be found</exception>
    public SharedFramework[] GetSharedFrameworkConfiguration(string framework, Version version)
    {
        (string referencePath, string implementationPath)
            = _dotnetInstallationLocator.FindInstallation(framework, version);

        IReadOnlyCollection<MetadataReference> referenceDlls = CreateDefaultReferences(
            referencePath,
            Directory.GetFiles(referencePath, "*.dll", SearchOption.TopDirectoryOnly)
        );
        IReadOnlyCollection<MetadataReference> implementationDlls = CreateDefaultReferences(
            implementationPath,
            Directory.GetFiles(implementationPath, "*.dll", SearchOption.TopDirectoryOnly)
        );

        // Microsoft.NETCore.App is always loaded.
        // e.g. if we're loading Microsoft.AspNetCore.App, load it alongside Microsoft.NETCore.App.
        return framework switch
        {
            SharedFramework.NET_CORE_APP => new[]
            {
                new SharedFramework(
                    referencePath,
                    referenceDlls,
                    implementationPath,
                    implementationDlls
                ),
            },
            _ => GetSharedFrameworkConfiguration(SharedFramework.NET_CORE_APP, version)
                .Append(
                    new SharedFramework(
                        referencePath,
                        referenceDlls,
                        implementationPath,
                        implementationDlls
                    )
                )
                .ToArray(),
        };
    }

    public void LoadSharedFrameworkConfiguration(string framework, Version version)
    {
        SharedFramework[] sharedFrameworks = GetSharedFrameworkConfiguration(framework, version);
        LoadSharedFrameworkConfiguration(sharedFrameworks);
    }

    public void LoadSharedFrameworkConfiguration(SharedFramework[] sharedFrameworks)
    {
        _referenceAssemblyPaths.UnionWith(
            sharedFrameworks.Select(framework => framework.ReferencePath)
        );
        _implementationAssemblyPaths.UnionWith(
            sharedFrameworks.Select(framework => framework.ImplementationPath)
        );
        _sharedFrameworkImplementationAssemblyPaths.UnionWith(
            sharedFrameworks.Select(framework => framework.ImplementationPath)
        );
        _loadedReferenceAssemblies.UnionWith(
            sharedFrameworks.SelectMany(framework => framework.ReferenceAssemblies)
        );
        _loadedImplementationAssemblies.UnionWith(
            sharedFrameworks.SelectMany(framework => framework.ImplementationAssemblies)
        );
    }

    internal void AddImplementationAssemblyReferences(IEnumerable<MetadataReference> references)
    {
        IEnumerable<string> paths = references
            .Select(
                r => Path.GetDirectoryName(r.Display) ?? r.Display
            ) // GetDirectoryName returns null when at root directory
            .WhereNotNull();

        _implementationAssemblyPaths.UnionWith(paths);
        _loadedImplementationAssemblies.UnionWith(references);
    }

    internal IReadOnlyCollection<MetadataReference> EnsureReferenceAssemblyWithDocumentation(
        IReadOnlyCollection<MetadataReference> references
    )
    {
        _loadedReferenceAssemblies.UnionWith(
            references.Select(suppliedReference => EnsureReferenceAssembly(suppliedReference))
                .WhereNotNull()
        );

        return _loadedReferenceAssemblies;
    }

    internal IReadOnlyCollection<UsingDirectiveSyntax> GetUsings(string code)
        => CSharpSyntaxTree.ParseText(code)
            .GetRoot()
            .DescendantNodes()
            .OfType<UsingDirectiveSyntax>()
            .ToList();

    internal void TrackUsings(IReadOnlyCollection<UsingDirectiveSyntax> usingsToAdd)
    {
        _usings.UnionWith(usingsToAdd);
    }

    private readonly ConcurrentDictionary<string, MetadataReference> _cachedMetadataReferences;
    private readonly DotNetInstallationLocator _dotnetInstallationLocator;
    private readonly HashSet<string> _implementationAssemblyPaths;
    private readonly HashSet<MetadataReference> _loadedImplementationAssemblies;
    private readonly HashSet<MetadataReference> _loadedReferenceAssemblies;
    private readonly HashSet<string> _referenceAssemblyPaths;
    private readonly HashSet<string> _sharedFrameworkImplementationAssemblyPaths;
    private readonly HashSet<UsingDirectiveSyntax> _usings;

    private IReadOnlyCollection<MetadataReference> CreateDefaultReferences(
        string assemblyPath,
        IReadOnlyCollection<string> assemblies
    )
    {
        return assemblies.AsParallel()
            .Select(
                dll =>
                {
                    string fullReferencePath = Path.Combine(assemblyPath, dll);
                    string fullDocumentationPath = Path.ChangeExtension(fullReferencePath, ".xml");

                    if (!AssemblyReferenceService.IsManagedAssembly(fullReferencePath))
                    {
                        return null;
                    }

                    return File.Exists(fullDocumentationPath)
                        ? MetadataReference.CreateFromFile(
                            fullReferencePath,
                            documentation: XmlDocumentationProvider.CreateFromFile(
                                fullDocumentationPath
                            )
                        )
                        : MetadataReference.CreateFromFile(fullReferencePath);
                }
            )
            .WhereNotNull()
            .ToList();
    }

    /// <summary>
    ///     Resolves a bit of a mismatch: the scripting APIs use implementation assemblies only. The
    ///     general workspace/project
    ///     roslyn APIs use the reference
    ///     assemblies. We don't want to accidentally use an implementation assembly with the workspace
    ///     APIs, so do some
    ///     "best-effort" conversion
    ///     here. If it's a reference assembly, pass it through unchanged. If it's an implementation
    ///     assembly, try to locate
    ///     the corresponding reference assembly.
    /// </summary>
    /// <remarks>
    ///     This method can run in multiple threads due to the "main" thread and the "background
    ///     initialization" thread.
    /// </remarks>
    private MetadataReference? EnsureReferenceAssembly(MetadataReference reference)
    {
        string? suppliedAssemblyPath = reference.Display;

        if (suppliedAssemblyPath is null) // if we don't have an assembly path or display, we won't make any decision because we have nothing to go on.
        {
            return reference;
        }

        if (_cachedMetadataReferences.TryGetValue(
                suppliedAssemblyPath,
                out MetadataReference? cachedReference
            ))
        {
            return cachedReference;
        }

        // it's already a reference assembly, just cache it and use it.
        if (_referenceAssemblyPaths.Any(path => suppliedAssemblyPath.StartsWith(path)))
        {
            _cachedMetadataReferences[suppliedAssemblyPath] = reference;

            return reference;
        }

        // it's probably an implementation assembly, find the corresponding reference assembly and documentation if we can.

        string suppliedAssemblyFileName = Path.GetFileName(suppliedAssemblyPath);
        var suppliedAssemblyName = AssemblyName.GetAssemblyName(suppliedAssemblyPath)
            .ToString();
        string assembly = _referenceAssemblyPaths
                              .Select(path => Path.Combine(path, suppliedAssemblyFileName))
                              .FirstOrDefault(
                                  potentialReferencePath => File.Exists(potentialReferencePath) &&
                                                            (AssemblyName.GetAssemblyName(
                                                                     potentialReferencePath
                                                                 )
                                                                 .ToString() ==
                                                             suppliedAssemblyName)
                              ) ??
                          suppliedAssemblyPath;

        if (_sharedFrameworkImplementationAssemblyPaths.Any(path => assembly.StartsWith(path)))
        {
            return null;
        }

        string potentialDocumentationPath = Path.ChangeExtension(assembly, ".xml");
        XmlDocumentationProvider? documentation = File.Exists(potentialDocumentationPath)
            ? XmlDocumentationProvider.CreateFromFile(potentialDocumentationPath)
            : null;

        PortableExecutableReference completeMetadataReference
            = MetadataReference.CreateFromFile(assembly, documentation: documentation);
        _cachedMetadataReferences[suppliedAssemblyPath] = completeMetadataReference;

        return completeMetadataReference;
    }

    private static (string framework, Version version) GetDesiredFrameworkVersion(
        string sharedFramework
    )
    {
        string[] parts = sharedFramework.Split('/');

        return parts.Length switch
        {
            1 => (parts[0], Environment.Version),
            2 => (parts[0], new Version(parts[1])),
            _ => throw new InvalidOperationException(
                "Unknown Shared Framework configuration: " + sharedFramework
            ),
        };
    }

    private static bool IsManagedAssembly(string assemblyPath)
    {
        try
        {
            _ = AssemblyName.GetAssemblyName(assemblyPath);

            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }
}
