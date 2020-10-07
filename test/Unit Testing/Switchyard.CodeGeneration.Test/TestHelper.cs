using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Switchyard.CodeGeneration.Test
{
    public static class TestHelper
    {
        public static (AdhocWorkspace workspace, Document document) CreateWorkspace(string sourceText)
        {
            var workspace = new AdhocWorkspace();
            var projectInfo = ProjectInfo.Create(id: ProjectId.CreateNewId(), version: VersionStamp.Create(),
                name: "NewProject", assemblyName: "NewProject", language: LanguageNames.CSharp);
            var newProject = workspace.AddProject(projectInfo);
            var document = workspace.AddDocument(newProject.Id, "NewFile.cs", SourceText.From(sourceText));
            return (workspace, document);
        }
    }
}