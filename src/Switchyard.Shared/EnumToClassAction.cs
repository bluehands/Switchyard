using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FunicularSwitch;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Switchyard.CodeGeneration;
using Task = System.Threading.Tasks.Task;

namespace Switchyard
{
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

        public static bool HasSuggestedActions(Option<SyntaxToken> range) =>
             GetEnumDeclarationIfInRange(range) != null;

        public static EnumDeclarationSyntax GetEnumDeclarationIfInRange(Option<SyntaxToken> token)
        {
            return token.Bind(t =>
            {
                if (t.Parent != null)
                {
                    foreach (var node in t.Parent.AncestorsAndSelf())
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

                return Option<EnumDeclarationSyntax>.None;
            }).GetValueOrDefault();
        }
    }
}