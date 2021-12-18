// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;

namespace CSharpRepl.Services.Roslyn;

using System.Collections.Generic;
using System.Linq;

using Extensions;

using Logging;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;

using References;

using Scripting;

/// <summary>
///     Editor services like code completion and syntax highlighting require the roslyn
///     workspace/project/document model.
///     Evaluated script code becomes a document in a project, and then each subsequent evaluation adds
///     a new project and
///     document. This new project has a project reference back to the previous project.
///     In this way, the list of REPL submissions is a linked list of projects, where each project has
///     a single document
///     containing the REPL submission.
/// </summary>
internal sealed class WorkspaceManager
{
    public Document CurrentDocument { get; private set; }

    public WorkspaceManager(
        CSharpCompilationOptions compilationOptions,
        AssemblyReferenceService referenceAssemblyService,
        ITraceLogger logger
    )
    {
        _compilationOptions = compilationOptions;
        _referenceAssemblyService = referenceAssemblyService;
        _logger = logger;
        _workspace = new AdhocWorkspace(MefHostServices.Create(MefHostServices.DefaultAssemblies));

        logger.Log(
            () => "MEF Default Assemblies: " +
                  string.Join(", ", MefHostServices.DefaultAssemblies.Select(a => a.Location))
        );

        IReadOnlyCollection<MetadataReference> assemblyReferences
            = referenceAssemblyService.EnsureReferenceAssemblyWithDocumentation(
                referenceAssemblyService.LoadedReferenceAssemblies
            );

        Document? document = WorkspaceManager.EmptyProjectAndDocumentChangeset(
                _workspace.CurrentSolution,
                assemblyReferences,
                compilationOptions,
                out DocumentId documentId
            )
            .ApplyChanges(_workspace)
            .GetDocument(documentId);

        if (document is null)
        {
            logger.Log(
                () => "Null document detected during initialization. Project MetadataReferences: " +
                      string.Join(", ", assemblyReferences.Select(r => r.Display))
            );

            throw new InvalidOperationException(WorkspaceManager.ROSLYN_WORKSPACE_ERROR_FORMAT);
        }

        CurrentDocument = document;
    }

    public void UpdateCurrentDocument(EvaluationResult.Success result)
    {
        IReadOnlyCollection<MetadataReference> assemblyReferences
            = _referenceAssemblyService.EnsureReferenceAssemblyWithDocumentation(result.References);
        Document? document = WorkspaceManager.EmptyProjectAndDocumentChangeset(
                _workspace.CurrentSolution,
                assemblyReferences,
                _compilationOptions,
                out DocumentId documentId
            )
            .WithDocumentText(CurrentDocument.Id, SourceText.From(result.Input))
            .ApplyChanges(_workspace)
            .GetDocument(documentId);

        if (document is null)
        {
            _logger.Log(
                () => "Null document detected during update. Project MetadataReferences: " +
                      string.Join(", ", assemblyReferences.Select(r => r.Display))
            );

            throw new InvalidOperationException(WorkspaceManager.ROSLYN_WORKSPACE_ERROR_FORMAT);
        }

        CurrentDocument = document;
    }

    private readonly CSharpCompilationOptions _compilationOptions;
    private readonly ITraceLogger _logger;
    private readonly AssemblyReferenceService _referenceAssemblyService;
    private readonly AdhocWorkspace _workspace;

    private static DocumentInfo CreateDocument(ProjectInfo projectInfo, string text)
        => DocumentInfo.Create(
            DocumentId.CreateNewId(projectInfo.Id),
            projectInfo.Name + "Script",
            sourceCodeKind: SourceCodeKind.Script,
            loader: TextLoader.From(
                TextAndVersion.Create(SourceText.From(text), VersionStamp.Create())
            )
        );

    private static ProjectInfo CreateProject(
        Solution solution,
        IReadOnlyCollection<MetadataReference> references,
        CompilationOptions compilationOptions
    )
    {
        return ProjectInfo.Create(
                ProjectId.CreateNewId(),
                VersionStamp.Create(),
                "Project" + DateTime.UtcNow.Ticks,
                "Project" + DateTime.UtcNow.Ticks,
                compilationOptions.Language,
                isSubmission: true
            )
            .WithMetadataReferences(references)
            .WithProjectReferences(
                solution.ProjectIds.TakeLast(1)
                    .Select(id => new ProjectReference(id))
            )
            .WithCompilationOptions(compilationOptions);
    }

    private static Solution EmptyProjectAndDocumentChangeset(
        Solution solution,
        IReadOnlyCollection<MetadataReference> references,
        CompilationOptions compilationOptions,
        out DocumentId documentId
    )
    {
        ProjectInfo projectInfo = WorkspaceManager.CreateProject(
            solution,
            references,
            compilationOptions
        );
        DocumentInfo documentInfo = WorkspaceManager.CreateDocument(projectInfo, string.Empty);

        documentId = documentInfo.Id;

        return solution.AddProject(projectInfo)
            .AddDocument(documentInfo);
    }

    private const string ROSLYN_WORKSPACE_ERROR_FORMAT
        = @"Could not initialize Roslyn Workspace. Please consider reporting a bug to https://github.com/waf/CSharpRepl, after running ""csharprepl --trace"" to produce a log file in the current directory.";
}
