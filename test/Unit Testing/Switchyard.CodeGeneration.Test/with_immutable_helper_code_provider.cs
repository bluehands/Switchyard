using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Document = Microsoft.CodeAnalysis.Document;

namespace Switchyard.CodeGeneration.Test;

public abstract class CodeProviderSpec : AsyncTestSpecification
{
	protected string SourceText = null!;
	protected string Updated = null!;

	protected override void Given()
	{
		SourceText = WithSource();
	}

	protected override async Task When()
	{
		var (workspace, document) = TestHelper.CreateWorkspace(SourceText);
		var root = await document.GetSyntaxRootAsync().ConfigureAwait(false);
		await Refactor(workspace, document, root!).ConfigureAwait(false);
		Updated = (await workspace.CurrentSolution.GetDocument(document.Id)!.GetSyntaxRootAsync().ConfigureAwait(false))!.ToFullString();
		await VerifyGenerationResult(Updated);

	}

	protected abstract Task Refactor(AdhocWorkspace workspace, Document document, SyntaxNode root);

	protected abstract string WithSource();

	[TestMethod]
	public void Then_refactored_output_compiles_without_errors()
	{
	}
}

[TestClass]
public abstract class with_immutable_helper_code_provider : CodeProviderSpec
{
	protected override async Task Refactor(AdhocWorkspace workspace, Document document, SyntaxNode root)
	{
		var updatedDocument = await ImmutableHelpersCodeProvider.GenerateWithExtension(
				document,
				root.DescendantNodes().OfType<ClassDeclarationSyntax>().Last(), CancellationToken.None)
			.ConfigureAwait(false);

		workspace.TryApplyChanges(updatedDocument.Project.Solution);
	}
}

[TestClass]
public class When_adding_with_extension_for_top_level_class : with_immutable_helper_code_provider
{
	protected override string WithSource() => @"using System;

namespace Test
{
	//fake Option class to avoid real FunicularSwitch reference in Test
	public class Option<T> 
	{
		public T1 Match<T1>(Func<T, T1> some, Func<T1> none) => throw new NotImplementedException();
	}

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
}

[TestClass]
public class When_adding_with_extension_for_nested_class : with_immutable_helper_code_provider
{
	protected override string WithSource() => @"
using System;

namespace Test
{
	//fake Option class to avoid real FunicularSwitch reference in Test
	public class Option<T> 
	{
		public T1 Match<T1>(Func<T, T1> some, Func<T1> none) => throw new NotImplementedException();
	}

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
}