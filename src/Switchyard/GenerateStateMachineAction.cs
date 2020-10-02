using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Switchyard.CodeGeneration;
using Task = System.Threading.Tasks.Task;

namespace Switchyard
{
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

        public static bool HasSuggestedActions(SnapshotSpan range) => range.TryGetDotFilename().IsSome();
    }
}