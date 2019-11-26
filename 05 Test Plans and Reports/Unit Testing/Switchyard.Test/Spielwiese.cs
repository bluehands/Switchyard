using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Switchyard.CodeGeneration;

namespace StateMachineGenerator.Test
{
    [TestClass]
    public class Spiel
    {
        [TestMethod]
        public async Task Wiese()
        {
            var workspace = new AdhocWorkspace();
            var projectInfo = ProjectInfo.Create(id: ProjectId.CreateNewId(), 
                version: VersionStamp.Create(), 
                name: "NewProject", 
                assemblyName: "NewProject", 
                language: LanguageNames.CSharp);
            var newProject = workspace.AddProject(projectInfo);
            var sourceText = SourceText.From(@"
namespace StateMachineGenerator.Test
{
    class A 
    {
    }
}
");
            var newDocument = workspace.AddDocument(newProject.Id, "NewFile.cs", sourceText);

            var root = await newDocument.GetSyntaxRootAsync().ConfigureAwait(false);

            //var ns = root.DescendantNodes().OfType<NamespaceDeclarationSyntax>().First();
            //ns = ns.WithMembers(ns.Members.Insert(0, SyntaxFactory.ClassDeclaration("B").Public()));

            root = root.AddOrUpdateNode(c => c.Name() == "B", () => SyntaxFactory.ClassDeclaration("B").Public(), c => c);

            root = root.Format(workspace);
        }   
    }
}
