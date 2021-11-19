using Microsoft.CodeAnalysis;

namespace Switchyard.CodeGeneration
{
    public static class WorkspaceExtension
    {
        public static void UpdateRoot(this Workspace workspace, Document document, SyntaxNode documentRoot)
        {
            documentRoot = documentRoot.Format(workspace);
            documentRoot = documentRoot.SingleLineProperties();
            var newDocument = document.WithSyntaxRoot(documentRoot);
            workspace.TryApplyChanges(newDocument.Project.Solution);
        }
    }
}