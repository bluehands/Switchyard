using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FunicularSwitch;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Utilities;
using Switchyard.CodeGeneration;

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

            var token = ThreadHelper.JoinableTaskFactory.Run(() => range.GetSyntaxTokenAtRangeStart(cancellationToken));

            var enumDeclaration = EnumToClassAction.GetEnumDeclarationIfInRange(token);
            if (enumDeclaration != null)
            {
                yield return new SuggestedActionSet("Any", new[] { new EnumToClassAction(enumDeclaration, new UnionTypeCodeProvider(m_Workspace), document) });
            }

            var classDeclaration = GenerateWithAction.GetClassDeclarationIfInRange(token);
            if (classDeclaration != null)
            {
                yield return new SuggestedActionSet("Any", new[] { new GenerateWithAction(classDeclaration, new ImmutableHelpersCodeProvider(m_Workspace), document) });
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
            var token = await range.GetSyntaxTokenAtRangeStart(cancellationToken).ConfigureAwait(false);

            return EnumToClassAction.HasSuggestedActions(token)
                   || GenerateWithAction.HasSuggestedActions(token)
                   || GenerateStateMachineAction.HasSuggestedActions(range);

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

        public static async Task<Option<SyntaxToken>> GetSyntaxTokenAtRangeStart(this SnapshotSpan range, CancellationToken cancellationToken)
        {
            var document = range.Snapshot.TextBuffer.GetRelatedDocuments().FirstOrDefault();
            var token = Option.None<SyntaxToken>();
            if (document != null)
            {
                if (document.TryGetSyntaxTree(out var syntaxTree))
                {
                    token = (await syntaxTree.GetRootAsync(cancellationToken).ConfigureAwait(false)).FindToken(range.Start);
                }
            }

            return token;
        }
    }
}