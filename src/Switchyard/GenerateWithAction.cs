using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Switchyard.CodeGeneration;
using Task = System.Threading.Tasks.Task;

namespace Switchyard
{
    public class GenerateWithAction : ISuggestedAction
    {
        readonly ClassDeclarationSyntax m_ClassDeclaration;
        readonly Document m_Document;
        readonly ImmutableHelpersCodeProvider m_CodeProvider;

        public GenerateWithAction(ClassDeclarationSyntax classDeclaration, Document document)
        {
            m_ClassDeclaration = classDeclaration;
            m_Document = document;
        }

        public bool HasActionSets => false;
        public string DisplayText => "Generate 'With' Extension";
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
            ThreadHelper.JoinableTaskFactory.Run(() => m_CodeProvider.GenerateWithExtension(m_Document, m_ClassDeclaration, cancellationToken));
        }

        public bool TryGetTelemetryId(out Guid telemetryId)
        {
            telemetryId = default;
            return false;
        }

        public static async Task<bool> HasSuggestedActions(ISuggestedActionCategorySet requestedActionCategories,
            SnapshotSpan range, CancellationToken cancellationToken) =>
            await GetClassDeclarationIfInRange(range, cancellationToken).ConfigureAwait(false) != null;

        public static async Task<ClassDeclarationSyntax> GetClassDeclarationIfInRange(SnapshotSpan range, CancellationToken cancellationToken)
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
                        case ClassDeclarationSyntax classDeclaration:
                            return classDeclaration;
                    }
                }
            }

            return null;
        }
    }
}
