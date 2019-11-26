using Microsoft.CodeAnalysis;

namespace Switchyard.CodeGeneration
{
    static class WorkspaceExtension
    {
        public static void UpdateRoot(this Workspace workspace, Document document, SyntaxNode documentRoot)
        {
            documentRoot = documentRoot.Format(workspace);
            var newDocument = document.WithSyntaxRoot(documentRoot);
            workspace.TryApplyChanges(newDocument.Project.Solution);
        }
    }
}