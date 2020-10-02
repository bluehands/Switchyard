using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FunicularSwitch;
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

        public GenerateWithAction(ClassDeclarationSyntax classDeclaration, ImmutableHelpersCodeProvider codeProvider, Document document)
        {
            m_ClassDeclaration = classDeclaration;
            m_Document = document;
            m_CodeProvider = codeProvider;
        }

        public bool HasActionSets => false;
        public string DisplayText => "Generate 'With' extension";
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

        public static bool HasSuggestedActions(
            Option<SyntaxToken> token) => GetClassDeclarationIfInRange(token) != null;

        public static ClassDeclarationSyntax GetClassDeclarationIfInRange(Option<SyntaxToken> token) =>
            token.Bind(t =>
            {
                if (t.Parent != null)
                {
                    foreach (var node in t.Parent.AncestorsAndSelf())
                    {
                        switch (node)
                        {
                            case ClassDeclarationSyntax classDeclaration:
                                return classDeclaration;
                        }
                    }
                }

                return Option<ClassDeclarationSyntax>.None;
            }).GetValueOrDefault();
    }
}
