using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Utilities;
using Switchyard.CodeGeneration;
using Task = System.Threading.Tasks.Task;

namespace Switchyard
{
    [Export(typeof(ISuggestedActionsSourceProvider))]
    [Name("StateMachineGenerator")]
    [ContentType("code")]
    public class ActionsSourceProvider : ISuggestedActionsSourceProvider
    {
        readonly Workspace m_Workspace;

        [Import(typeof(ITextStructureNavigatorSelectorService))] internal ITextStructureNavigatorSelectorService NavigatorService { get; set; }

        [ImportingConstructor]
        public ActionsSourceProvider([Import(typeof(VisualStudioWorkspace), AllowDefault = true)] Workspace workspace) => m_Workspace = workspace;

        public ISuggestedActionsSource CreateSuggestedActionsSource(ITextView textView, ITextBuffer textBuffer) => new ActionSource(m_Workspace);
    }

    public class ActionSource : ISuggestedActionsSource
    {
        readonly Workspace m_Workspace;

        public ActionSource(Workspace workspace) => m_Workspace = workspace;

#pragma warning disable CS0067
        public event EventHandler<EventArgs> SuggestedActionsChanged;
#pragma warning restore CS0067

        public void Dispose()
        {
        }

        public IEnumerable<SuggestedActionSet> GetSuggestedActions(ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken)
        {
            var document = range.Snapshot.TextBuffer.GetRelatedDocuments().FirstOrDefault();
            if (document == null)
                yield break;

            var enumDeclaration = ThreadHelper.JoinableTaskFactory.Run(() => EnumToClassAction.GetEnumDeclarationIfInRange(range, cancellationToken));
            if (enumDeclaration != null)
            {
                yield return new SuggestedActionSet("Any", new[] { new EnumToClassAction(enumDeclaration, new UnionTypeCodeProvider(m_Workspace), document) });
            }

            var dotFileName = range.TryGetDotFilename();
            if (!dotFileName.IsSome())
            {
                yield break;
            }
            yield return new SuggestedActionSet("Any", new[] { new GenerateStateMachineAction(dotFileName.GetValueOrThrow(), new StateMachineCodeProvider(m_Workspace), document) });
        }

        public async Task<bool> HasSuggestedActionsAsync(ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken)
        {
            var hasActionResults = await Task.WhenAll(
                GenerateStateMachineAction.HasSuggestedActions(requestedActionCategories, range, cancellationToken),
                EnumToClassAction.HasSuggestedActions(requestedActionCategories, range, cancellationToken)
                ).ConfigureAwait(false);
            return hasActionResults.Any();
        }

        public bool TryGetTelemetryId(out Guid telemetryId)
        {
            telemetryId = default;
            return false;
        }
    }

    public static class RangeExtension
    {
        public static Option<string> TryGetDotFilename(this SnapshotSpan range)
        {
            var document = range.Snapshot.TextBuffer.GetRelatedDocuments().FirstOrDefault();
            if (document == null)
            {
                return Option<string>.None;
            }

            var dotFilePath = Path.ChangeExtension(document.FilePath, "dot");
            var dotFileFound = File.Exists(dotFilePath);
            return dotFileFound ? dotFilePath : Option<string>.None;
        }
    }

    public class GenerateStateMachineAction : ISuggestedAction
    {
        readonly string m_DotFileName;
        readonly StateMachineCodeProvider m_CodeProvider;
        readonly Document m_Document;

        public GenerateStateMachineAction(string dotFileName, StateMachineCodeProvider codeProvider, Document document)
        {
            m_DotFileName = dotFileName;
            m_CodeProvider = codeProvider;
            m_Document = document;
        }

        public bool HasActionSets => false;
        public string DisplayText => $"Generate state machine from {Path.GetFileName(m_DotFileName)}";
        public object IconMoniker => string.Empty;
        public string IconAutomationText => string.Empty;
        public string InputGestureText => string.Empty;
        public bool HasPreview => false;
        ImageMoniker ISuggestedAction.IconMoniker => default;

        public void Dispose()
        {
        }

        public Task<IEnumerable<SuggestedActionSet>> GetActionSetsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IEnumerable<SuggestedActionSet>>(new[] { new SuggestedActionSet("Any", new[] { this }) });
        }

        public Task<object> GetPreviewAsync(CancellationToken cancellationToken) => Task.FromResult<object>(null);

        public void Invoke(CancellationToken cancellationToken)
        {
            ThreadHelper.JoinableTaskFactory.Run(() => m_CodeProvider.GenerateStateMachine(m_Document, m_DotFileName, cancellationToken));
        }

        public bool TryGetTelemetryId(out Guid telemetryId)
        {
            telemetryId = default;
            return false;
        }

        public static Task<bool> HasSuggestedActions(ISuggestedActionCategorySet requestedActionCategories,
            SnapshotSpan range, CancellationToken cancellationToken) =>
            Task.FromResult(range.TryGetDotFilename().IsSome());
    }

    public class EnumToClassAction : ISuggestedAction
    {
        readonly EnumDeclarationSyntax m_EnumDeclaration;
        readonly UnionTypeCodeProvider m_CodeProvider;
        readonly Document m_Document;

        public EnumToClassAction(EnumDeclarationSyntax enumDeclaration, UnionTypeCodeProvider codeProvider, Document document)
        {
            m_EnumDeclaration = enumDeclaration;
            m_CodeProvider = codeProvider;
            m_Document = document;
        }

        public bool HasActionSets => false;
        public string DisplayText => "Expand enum to union type";
        public object IconMoniker => string.Empty;
        public string IconAutomationText => string.Empty;
        public string InputGestureText => string.Empty;
        public bool HasPreview => false;
        ImageMoniker ISuggestedAction.IconMoniker => default;

        public void Dispose()
        {
        }

        public Task<IEnumerable<SuggestedActionSet>> GetActionSetsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IEnumerable<SuggestedActionSet>>(new[] { new SuggestedActionSet("Any", new[] { this }) });
        }

        public Task<object> GetPreviewAsync(CancellationToken cancellationToken) => Task.FromResult<object>(null);

        public void Invoke(CancellationToken cancellationToken)
        {
            ThreadHelper.JoinableTaskFactory.Run(() => m_CodeProvider.EnumToClass(m_Document, m_EnumDeclaration, cancellationToken));
        }

        public bool TryGetTelemetryId(out Guid telemetryId)
        {
            telemetryId = default;
            return false;
        }

        public static async Task<bool> HasSuggestedActions(ISuggestedActionCategorySet requestedActionCategories,
            SnapshotSpan range, CancellationToken cancellationToken) =>
            await GetEnumDeclarationIfInRange(range, cancellationToken).ConfigureAwait(false) != null;

        public static async Task<EnumDeclarationSyntax> GetEnumDeclarationIfInRange(SnapshotSpan range, CancellationToken cancellationToken)
        {
            var document = range.Snapshot.TextBuffer.GetRelatedDocuments().FirstOrDefault();
            if (document == null)
                return null;

            if (!document.TryGetSyntaxTree(out var syntaxTree))
                return null;

            var token = (await syntaxTree.GetRootAsync(cancellationToken).ConfigureAwait(false)).FindToken(range.Start);
            if (token.Parent != null)
            {
                foreach (var node in token.Parent.AncestorsAndSelf())
                {
                    switch (node)
                    {
                        case EnumDeclarationSyntax enumDeclaration:
                            return enumDeclaration;

                        case ClassDeclarationSyntax classDeclaration:
                            {
                                var enumDeclaration = classDeclaration.Members.OfType<EnumDeclarationSyntax>()
                                    .FirstOrDefault(e => e.Name() == WrapEnumToClass.DefaultNestedEnumTypeName);

                                if (enumDeclaration != null)
                                    return enumDeclaration;
                                break;
                            }
                    }
                }
            }

            return null;
        }
    }
}
