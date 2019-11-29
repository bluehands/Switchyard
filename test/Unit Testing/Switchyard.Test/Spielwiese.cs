using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Switchyard.CodeGeneration;
namespace StateMachineGenerator.Test
{
    public abstract class Test
    {
        public enum Ids
        {
            One,
            Two
        }

        public Ids Id
        {
            get;
        }

        Test(Ids id) => Id = id;

        public override string ToString() => Enum.GetName(typeof(Ids), Id) ?? Id.ToString();

        bool Equals(Test other) => Id == other.Id;

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != GetType())
                return false;
            return Equals((Test)obj);
        }

        public override int GetHashCode() => (int)Id;

        public static Test One() => new One_();
        public static readonly Test Two = new Two_();
        public class One_ : Test
        {
            public One_() : base(Ids.One)
            {
            }
        }

        public class Two_ : Test
        {
            public Two_() : base(Ids.Two)
            {
            }
        }
    }

    public static class TestExtension
    {
        public static T Match<T>(this Test test, Func<Test.One_, T> one, Func<Test.Two_, T> two)
        {
            switch (test.Id)
            {
                case Test.Ids.One:
                    return one((Test.One_)test);
                case Test.Ids.Two:
                    return two((Test.Two_)test);
                default:
                    throw new ArgumentException($"Unknown type implementing Test: {test.GetType().Name}");
            }
        }

        public static async Task<T> Match<T>(this Test test, Func<Test.One_, Task<T>> one, Func<Test.Two_, Task<T>> two)
        {
            switch (test.Id)
            {
                case Test.Ids.One:
                    return await one((Test.One_)test).ConfigureAwait(false);
                case Test.Ids.Two:
                    return await two((Test.Two_)test).ConfigureAwait(false);
                default:
                    throw new ArgumentException($"Unknown type implementing Test: {test.GetType().Name}");
            }
        }

        public static async Task<T> Match<T>(this Task<Test> test, Func<Test.One_, T> one, Func<Test.Two_, T> two) => (await test.ConfigureAwait(false)).Match(one, two);

        public static async Task<T> Match<T>(this Task<Test> test, Func<Test.One_, Task<T>> one, Func<Test.Two_, Task<T>> two) => await(await test.ConfigureAwait(false)).Match(one, two).ConfigureAwait(false);
    }

    [TestClass]
    public class Spiel
    {
        [TestMethod]
        public async Task Wiese()
        {
            var workspace = new AdhocWorkspace();
            var projectInfo = ProjectInfo.Create(id: ProjectId.CreateNewId(), version: VersionStamp.Create(), name: "NewProject", assemblyName: "NewProject", language: LanguageNames.CSharp);
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