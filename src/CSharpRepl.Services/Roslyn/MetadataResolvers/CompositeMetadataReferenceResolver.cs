// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;

namespace CSharpRepl.Services.Roslyn.MetadataResolvers;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;

/// <summary>
///     A <see cref="MetadataReferenceResolver" /> that is contained by the
///     <see cref="CompositeMetadataReferenceResolver" />.
///     It gets a chance to resolve a reference; if it doesn't, the next
///     <see cref="IIndividualMetadataReferenceResolver" /> is called.
/// </summary>
internal interface IIndividualMetadataReferenceResolver
{
    ImmutableArray<PortableExecutableReference> ResolveReference(
        string reference,
        string? baseFilePath,
        MetadataReferenceProperties properties,
        MetadataReferenceResolver compositeResolver
    );
}

/// <summary>
///     A top-level metadata resolver. We can only specify a single
///     <see cref="MetadataReferenceResolver" /> in roslyn
///     scripting.
///     This composite class delegates to individual implementations (nuget resolver, assembly
///     resolver, csproj resolver,
///     etc).
/// </summary>
internal sealed class CompositeMetadataReferenceResolver
    : MetadataReferenceResolver, IEquatable<CompositeMetadataReferenceResolver>
{
    public bool Equals(CompositeMetadataReferenceResolver? other)
        => (other != null) &&
           EqualityComparer<IIndividualMetadataReferenceResolver[]>.Default.Equals(
               _resolvers,
               other._resolvers
           );

    public CompositeMetadataReferenceResolver(
        params IIndividualMetadataReferenceResolver[] resolvers
    )
        => _resolvers = resolvers;

    public override bool Equals(object? other)
        => Equals(other as CompositeMetadataReferenceResolver);

    public override int GetHashCode()
        => HashCode.Combine(_resolvers);

    public override ImmutableArray<PortableExecutableReference> ResolveReference(
        string reference,
        string? baseFilePath,
        MetadataReferenceProperties properties
    )
    {
        reference = reference.Trim();

        foreach (IIndividualMetadataReferenceResolver resolver in _resolvers)
        {
            ImmutableArray<PortableExecutableReference> resolved = resolver.ResolveReference(
                reference,
                baseFilePath,
                properties,
                this
            );

            if (resolved.Any())
            {
                return resolved;
            }
        }

        return ImmutableArray<PortableExecutableReference>.Empty;
    }

    private readonly IIndividualMetadataReferenceResolver[] _resolvers;
}
