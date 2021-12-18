﻿// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;

namespace CSharpRepl.Services.Roslyn.MetadataResolvers;

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;

using Nuget;

using PrettyPrompt.Consoles;

/// <summary>
///     Resolves nuget references, e.g. #r "nuget: Newtonsoft.Json" or #r "nuget: Newtonsoft.Json,
///     13.0.1"
/// </summary>
internal sealed class NugetPackageMetadataResolver : IIndividualMetadataReferenceResolver
{
    public ImmutableArray<PortableExecutableReference> ResolveReference(
        string reference,
        string? baseFilePath,
        MetadataReferenceProperties properties,
        MetadataReferenceResolver compositeResolver
    )
    {
        // This is a bit of a kludge. roslyn does not yet support adding multiple references from a single ResolveReference call, which
        // can happen with nuget packages (because they can have multiple DLLs and dependencies). https://github.com/dotnet/roslyn/issues/6900
        // We still want to use the "mostly standard" syntax of `#r "nuget:PackageName"` though, so make this a no-op and install the package
        // in the InstallNugetPackage method instead. Additional benefit is that we can use "real async" rather than needing to block here.
        if (IsNugetReference(reference))
        {
            return _dummyPlaceholder;
        }

        return ImmutableArray<PortableExecutableReference>.Empty;
    }

    public NugetPackageMetadataResolver(IConsole console)
    {
        _nugetInstaller = new NugetPackageInstaller(console);
        _dummyPlaceholder = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        }.ToImmutableArray();
    }

    // roslyn trims the "#r" prefix when passing to the resolver, but it has the prefix when called from our ScriptRunner
    public Task<ImmutableArray<PortableExecutableReference>> InstallNugetPackageAsync(
        string reference,
        CancellationToken cancellationToken
    )
    {
        // we can be a bit loose in our parsing here, because we were more strict in IsNugetReference.
        // the 0th element will be the "nuget" keyword, which we ignore.
        string[] packageParts = reference.Split(
            new[]
            {
                "#r", "\"", ":", " ", ",", "/", "\\",
            },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );

        return packageParts.Length switch
        {
            2 => _nugetInstaller.InstallAsync(
                packageParts[1],
                cancellationToken: cancellationToken
            ),
            3 => _nugetInstaller.InstallAsync(
                packageParts[1],
                packageParts[2]
                    .TrimStart('v'),
                cancellationToken
            ),
            _ => throw new InvalidOperationException(
                @"Malformed nuget reference. Expected #r ""nuget: PackageName"" or #r ""nuget: PackageName, version"""
            ),
        };
    }

    public bool IsNugetReference(string reference)
        => reference.ToLowerInvariant() is string lowercased &&
           (lowercased.StartsWith(NugetPackageMetadataResolver.NUGET_PREFIX) ||
            lowercased.StartsWith($"#r \"{NugetPackageMetadataResolver.NUGET_PREFIX}"));

    private readonly ImmutableArray<PortableExecutableReference> _dummyPlaceholder;
    private readonly NugetPackageInstaller _nugetInstaller;
    private const string NUGET_PREFIX = "nuget:";
}
