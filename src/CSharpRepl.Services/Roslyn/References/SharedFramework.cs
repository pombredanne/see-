// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;

namespace CSharpRepl.Services.Roslyn.References;

using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;

/// <summary>
///     Represents an installed Shared Framework. Can be a base framework (Microsoft.NETCore.App),
///     ASP.NET, Windows
///     Desktop, etc.
///     https://docs.microsoft.com/en-us/aspnet/core/fundamentals/metapackage-app?view=aspnetcore-5.0
/// </summary>
public class SharedFramework
{
    public string ReferencePath { get; }
    public string ImplementationPath { get; }
    public IReadOnlyCollection<MetadataReference> ReferenceAssemblies { get; }
    public IReadOnlyCollection<MetadataReference> ImplementationAssemblies { get; }

    public static string[] SupportedFrameworks { get; }
        = Path.GetDirectoryName(typeof(object).Assembly.Location) is string frameworkDirectory
            ? Directory.GetDirectories(Path.Combine(frameworkDirectory, "../../"))
                .Select(dir => Path.GetFileName(dir))
                .ToArray()
            : Array.Empty<string>();

    public SharedFramework(
        string referencePath,
        IReadOnlyCollection<MetadataReference> referenceAssemblies,
        string implementationPath,
        IReadOnlyCollection<MetadataReference> implementationAssemblies
    )
    {
        ReferencePath = referencePath;
        ImplementationPath = implementationPath;
        ReferenceAssemblies = referenceAssemblies;
        ImplementationAssemblies = implementationAssemblies;
    }

    public const string NET_CORE_APP = "Microsoft.NETCore.App";

    public static Version ToDotNetVersion(string version)
        // discard trailing preview versions, e.g. 6.0.0-preview.4.21253.7 
        => new(
            version.Split('-', 2)
                .First()
        );
}
