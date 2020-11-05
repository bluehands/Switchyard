using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Document = Microsoft.CodeAnalysis.Document;

namespace Switchyard.CodeGeneration.Test
{
    public abstract class CodeProviderSpec : AsyncTestSpecification
    {
        protected string SourceText;
        protected string Updated;

        protected override void Given()
        {
            SourceText = WithSource();
        }

        protected override async Task When()
        {
            var (workspace, document) = TestHelper.CreateWorkspace(SourceText);
            var root = await document.GetSyntaxRootAsync().ConfigureAwait(false);
            await Refactor(workspace, document, root).ConfigureAwait(false);
            Updated = (await workspace.CurrentSolution.GetDocument(document.Id).GetSyntaxRootAsync().ConfigureAwait(false)).ToFullString();
        }

        protected abstract Task Refactor(AdhocWorkspace workspace, Document document, SyntaxNode root);

        protected abstract string WithSource();
    }

    [TestClass]
    public abstract class with_immutable_helper_code_provider : CodeProviderSpec
    {
        protected override async Task Refactor(AdhocWorkspace workspace, Document document, SyntaxNode root)
        {
            var codeProvider = new ImmutableHelpersCodeProvider(workspace);
            await codeProvider.GenerateWithExtension(
                    document,
                    root.DescendantNodes().OfType<ClassDeclarationSyntax>().Last(), CancellationToken.None)
                .ConfigureAwait(false);
        }
    }

    [TestClass]
    public class When_adding_with_extension_for_top_level_class : with_immutable_helper_code_provider
    {
        protected override string WithSource() => @"namespace Test
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
}";

        [TestMethod]
        public void Then_correct_and_well_formatted_output_is_created()
        {
            Updated.Should().EndWith(@"
    public static class DummyWithExtension
    {
        public static Dummy With(this Dummy dummy, Option<string> prop1 = null, Option<bool> prop2 = null) => new Dummy(prop1: prop1 != null ? prop1.Match(x => x, () => dummy.Prop1) : dummy.Prop1, prop2: prop2 != null ? prop2.Match(x => x, () => dummy.Prop2) : dummy.Prop2);
    }
}");
        }
    }

    [TestClass]
    public class When_adding_with_extension_for_nested_class : with_immutable_helper_code_provider
    {
        protected override string WithSource() => @"namespace Test
{
    public class Parent
    {
        public class Child
        {
            public string Prop1 { get; }
            public bool Prop2 { get; }

            public Child(string prop1, bool prop2)
            {
                Prop1 = prop1;
                Prop2 = prop2;
            }
        }
    }
}";

        [TestMethod]
        public void Then_correct_and_well_formatted_output_is_created()
        {
            Updated.Should().EndWith(@"
    public static class ParentChildWithExtension
    {
        public static Parent.Child With(this Parent.Child child, Option<string> prop1 = null, Option<bool> prop2 = null) => new Parent.Child(prop1: prop1 != null ? prop1.Match(x => x, () => child.Prop1) : child.Prop1, prop2: prop2 != null ? prop2.Match(x => x, () => child.Prop2) : child.Prop2);
    }
}");
        }
    }

    [TestClass]
    public abstract class with_union_type_code_provider : CodeProviderSpec
    {
        protected override async Task Refactor(AdhocWorkspace workspace, Document document, SyntaxNode root)
        {
            var codeProvider = new UnionTypeCodeProvider(workspace);
            await codeProvider.EnumToClass(
                    document,
                    root.DescendantNodes().OfType<EnumDeclarationSyntax>().Last(), CancellationToken.None)
                .ConfigureAwait(false);
        }
    }

    [TestClass]
    public class When_generating_union_type_for_nested_enum : with_union_type_code_provider
    {
        protected override string WithSource() => @"namespace Test
{
    public class Parent
    {
        public enum Child
        {
            One,
            Two
        }
    }
}";

        [TestMethod]
        public void Then_assertion()
        {
            Updated.Should().NotBeNull();
        }
    }
}
