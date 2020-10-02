using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Document = Microsoft.CodeAnalysis.Document;

namespace Switchyard.CodeGeneration.Test
{
    [TestClass]
    public class ImmutableHelperCodeProviderTest
    {
        [TestMethod]
        public async Task Then_assertion()
        {
            var (workspace, document) = TestHelper.CreateWorkspace(
                @"namespace Test
{
    public class Dummy
    {
        public string Prop1 { get; }
        public bool Prop2 { get; }

        public Dummy(string prop1, bool prop2)
        {
            Prop1 = prop1;
            Prop2 = prop2;
        }
    }
}");

            var codeProvider = new ImmutableHelpersCodeProvider(workspace);
            var root = await document.GetSyntaxRootAsync().ConfigureAwait(false);
            await codeProvider.GenerateWithExtension(
                    document,
                    root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single(), CancellationToken.None)
                .ConfigureAwait(false);

            document = workspace.CurrentSolution.GetDocument(document.Id);

            root = await document.GetSyntaxRootAsync().ConfigureAwait(false);
            var updated = root.Format(workspace).ToFullString();
        }
    }

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

    public class MyImmutableObject
    {
        public int Number { get; }
        public bool IsActive { get; }
        public string Name { get; }

        public MyImmutableObject(int number, bool isActive, string name)
        {
            Number = number;
            IsActive = isActive;
            Name = name;
        }

        public void Test()
        {
            var bruno = new MyImmutableObject(42, true, "Bruno");
            var brunoInactivated = bruno.With(isActive: false);
        }
    }
    public static class MyImmutableObjectWithExtension
    {
        public static MyImmutableObject With(this MyImmutableObject myImmutableObject, Option<int> number = null, Option<bool> isActive = null, Option<string> name = null) => 
            new MyImmutableObject(
                number: number != null ? number.Match(x => x, () => myImmutableObject.Number) : myImmutableObject.Number, 
                isActive: isActive != null ? isActive.Match(x => x, () => myImmutableObject.IsActive) : myImmutableObject.IsActive, 
                name: name != null ? name.Match(x => x, () => myImmutableObject.Name) : myImmutableObject.Name
            );
    }
}
