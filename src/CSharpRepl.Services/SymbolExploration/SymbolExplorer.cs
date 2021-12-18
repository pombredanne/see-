// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;

namespace CSharpRepl.Services.SymbolExploration;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.SymbolStore;

using Roslyn.References;
using Roslyn.Scripting;

using SourceLink;

/// <summary>
///     Provides information (e.g. fully qualified names and SourceLink urls) of symbols.
/// </summary>
internal sealed class SymbolExplorer
{
    public SymbolExplorer(AssemblyReferenceService referenceService, ScriptRunner scriptRunner)
    {
        _scriptRunner = scriptRunner;
        _sourceLinkLookup = new SourceLinkLookup();
        _referenceService = referenceService;
        _displayOptions = new SymbolDisplayFormat(
            SymbolDisplayGlobalNamespaceStyle.Omitted,
            SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            SymbolDisplayGenericsOptions.None,
            SymbolDisplayMemberOptions.IncludeContainingType,
            SymbolDisplayDelegateStyle.NameOnly,
            SymbolDisplayExtensionMethodStyle.StaticMethod,
            SymbolDisplayParameterOptions.None,
            SymbolDisplayPropertyStyle.NameOnly,
            SymbolDisplayLocalOptions.None,
            SymbolDisplayKindOptions.None,
            SymbolDisplayMiscellaneousOptions.ExpandNullable
        );
    }

    public async Task<SymbolResult> LookupSymbolAtPosition(string text, int position)
    {
        ISymbol? symbolAtPosition = GetSymbolAtPosition(text, position);

        if (symbolAtPosition is null)
        {
            return SymbolResult.Unknown;
        }

        SymbolResult retValue = new(symbolAtPosition.ToDisplayString(_displayOptions), null);

        IAssemblySymbol? assembly = symbolAtPosition.ContainingAssembly;
        MetadataReader? assemblyReader = assembly?.GetMetadata()
            ?.GetModules()
            .FirstOrDefault()
            ?.GetMetadataReader();
        string? assemblyFilePath = _referenceService.LoadedImplementationAssemblies.LastOrDefault(
                a => !string.IsNullOrEmpty(a.Display) &&
                     (Path.GetFileNameWithoutExtension(a.Display) == assembly?.Identity.Name) &&
                     (AssemblyName.GetAssemblyName(a.Display)
                          .ToString() ==
                      assembly?.Identity.ToString())
            )
            ?.Display;

        if (assemblyReader is null ||
            assemblyFilePath is null)
        {
            return retValue;
        }

        TypeDefinition type = assemblyReader.TypeDefinitions
            .Select(t => assemblyReader.GetTypeDefinition(t))
            .FirstOrDefault(
                t => (assemblyReader.GetString(t.Namespace) ==
                      symbolAtPosition.ContainingNamespace.ToDisplayString()) &&
                     (assemblyReader.GetString(t.Name) ==
                      (symbolAtPosition.ContainingType?.Name ?? symbolAtPosition.Name))
            );

        using DebugSymbolLoader debugSymbolLoader = new(assemblyFilePath);

        foreach (SymbolStoreKey key in
                 debugSymbolLoader
                     .GetSymbolFileNames()) // we'll choose the first pdb we can successfully load. This will skip over e.g. Windows pdb files.
        {
            using SymbolStoreFile symbolFile
                = await debugSymbolLoader.DownloadSymbolFile(key, CancellationToken.None);

            MetadataReader? symbolReader = debugSymbolLoader.ReadPortablePdb(symbolFile);

            if (symbolReader is null)
            {
                continue;
            }

            // find the Sequence Points in the PDB that will give us document (e.g. filepath) and line numbers for the given method/property.
            SequencePointRange? sequencePointRange = SymbolExplorer.FindSequencePointRangeForSymbol(
                symbolReader,
                assemblyReader,
                type,
                symbolAtPosition
            );

            if (sequencePointRange is null)
            {
                continue;
            }

            // use source link the transform filepath to repository  url (e.g. Github).
            if (_sourceLinkLookup.TryGetSourceLinkUrl(
                    symbolReader,
                    sequencePointRange.Value,
                    out string? url
                ))
            {
                return retValue with
                {
                    Url = url,
                };
            }
        }

        return retValue;
    }

    private readonly SymbolDisplayFormat _displayOptions;
    private readonly AssemblyReferenceService _referenceService;
    private readonly ScriptRunner _scriptRunner;
    private readonly SourceLinkLookup _sourceLinkLookup;

    private static SequencePointRange? FallbackFirstSequencePoint(
        MetadataReader symbolReader,
        TypeDefinition type
    )
    {
        IEnumerable<SequencePoint> sequencePoints = type.GetMethods()
            .SelectMany(
                method => symbolReader.GetMethodDebugInformation(method)
                    .GetSequencePoints()
            )
            .Where(s => !s.IsHidden);

        return sequencePoints.Any()
            ? new SequencePointRange(sequencePoints.First(), null)
            : null;
    }

    private static SequencePointRange? FindEvent(
        MetadataReader symbolReader,
        MetadataReader assemblyReader,
        TypeDefinition type,
        IEventSymbol evt
    )
    {
        return SymbolExplorer.FindSequencePoint(
            symbolReader,
            type.GetEvents(),
            m => assemblyReader.GetEventDefinition(m),
            m => assemblyReader.GetString(m.Name) == evt.Name,
            m =>
            {
                EventAccessors accessors = assemblyReader.GetEventDefinition(m)
                    .GetAccessors();

                return !accessors.Adder.IsNil
                    ? accessors.Adder
                    : !accessors.Remover.IsNil
                        ? accessors.Remover
                        : !accessors.Raiser.IsNil
                            ? accessors.Raiser
                            : accessors.Others.First();
            }
        );
    }

    private static SequencePointRange? FindMethod(
        MetadataReader symbolReader,
        MetadataReader assemblyReader,
        TypeDefinition type,
        IMethodSymbol method
    )
    {
        return SymbolExplorer.FindSequencePoint(
            symbolReader,
            type.GetMethods(),
            m => assemblyReader.GetMethodDefinition(m),
            m => assemblyReader.GetString(m.Name) == method.Name,
            m => m
        );
    }

    private static SequencePointRange? FindProperty(
        MetadataReader symbolReader,
        MetadataReader assemblyReader,
        TypeDefinition type,
        IPropertySymbol method
    )
    {
        return SymbolExplorer.FindSequencePoint(
            symbolReader,
            type.GetProperties(),
            m => assemblyReader.GetPropertyDefinition(m),
            m => assemblyReader.GetString(m.Name) == method.Name,
            m =>
            {
                PropertyAccessors accessors = assemblyReader.GetPropertyDefinition(m)
                    .GetAccessors();

                return !accessors.Getter.IsNil
                    ? accessors.Getter
                    : !accessors.Setter.IsNil
                        ? accessors.Setter
                        : accessors.Others.First();
            }
        );
    }

    private static SequencePointRange? FindSequencePoint<TMember, TDefinition>(
        MetadataReader symbolReader,
        IEnumerable<TMember> members,
        Func<TMember, TDefinition> getDefinition,
        Predicate<TDefinition> matchByName,
        Func<TMember, MethodDefinitionHandle> toDefinitionHandle
    )
    {
        var memberDefinition = members.Select(
                m => new
                {
                    handle = m,
                    method = getDefinition(m),
                }
            )
            .FirstOrDefault(m => matchByName(m.method));

        if (memberDefinition is null)
        {
            return null;
        }

        SequencePointCollection sequencePoints = symbolReader
            .GetMethodDebugInformation(toDefinitionHandle(memberDefinition.handle))
            .GetSequencePoints();

        return sequencePoints.Any()
            ? new SequencePointRange(sequencePoints.First(), sequencePoints.Last())
            : null;
    }

    private static SequencePointRange? FindSequencePointRangeForSymbol(
        MetadataReader symbolReader,
        MetadataReader assemblyReader,
        TypeDefinition type,
        ISymbol symbolAtPosition
    )
    {
        return symbolAtPosition switch
               {
                   IMethodSymbol method => SymbolExplorer.FindMethod(
                       symbolReader,
                       assemblyReader,
                       type,
                       method
                   ),
                   IPropertySymbol prop => SymbolExplorer.FindProperty(
                       symbolReader,
                       assemblyReader,
                       type,
                       prop
                   ),
                   IEventSymbol evt => SymbolExplorer.FindEvent(
                       symbolReader,
                       assemblyReader,
                       type,
                       evt
                   ),
                   _ => null,
               } ??
               SymbolExplorer.FallbackFirstSequencePoint(symbolReader, type);
    }

    private ISymbol? GetSymbolAtPosition(string text, int position)
    {
        Compilation compilation = _scriptRunner.CompileTransient(text, OptimizationLevel.Debug);
        SyntaxTree tree = compilation.SyntaxTrees.Single();
        SemanticModel? semanticModel = compilation.GetSemanticModel(tree);

        if (semanticModel is null)
        {
            return null;
        }

        // the most obvious way to implement this would be using GetEnclosingSymbol or ChildThatContainsPosition.
        // however, neither of those appears to work for script-type projects. GetEnclosingSymbol always returns "<Initialize>".
        var symbols =
            from node in semanticModel.SyntaxTree.GetRoot()
                .DescendantNodes()
            where (node.Span.Start < position) && (position < node.Span.End)
            orderby node.Span.Length
            let symbolInfo = semanticModel.GetSymbolInfo(node)
            select new
            {
                node,
                symbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault(),
            };

        var mostSpecificSymbol = symbols.FirstOrDefault(s => s.symbol is not null);

        return mostSpecificSymbol?.symbol;
    }
}

public readonly struct SequencePointRange
{
    public SequencePointRange(SequencePoint start, SequencePoint? end)
    {
        Start = start;
        End = end;
    }

    public SequencePoint Start { get; }
    public SequencePoint? End { get; }
}

public record SymbolResult(string? SymbolDisplay, string? Url)
{
    public static readonly SymbolResult Unknown = new("Unknown", null);
}
