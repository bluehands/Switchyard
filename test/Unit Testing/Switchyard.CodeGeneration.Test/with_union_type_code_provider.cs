using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Switchyard.CodeGeneration.Test;

[TestClass]
public abstract class with_union_type_code_provider : CodeProviderSpec
{
	protected override async Task Refactor(AdhocWorkspace workspace, Document document, SyntaxNode root)
	{
		var updatedDoc = await UnionTypeCodeProvider.EnumToClass(
				document,
				root.DescendantNodes().OfType<EnumDeclarationSyntax>().Last(), CancellationToken.None)
			.ConfigureAwait(false);
		workspace.TryApplyChanges(updatedDoc.Project.Solution);
	}
}

[TestClass]
public class When_generating_union_type_for_nested_enum : with_union_type_code_provider
{
	protected override string WithSource() => @"
namespace Test
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
}

[TestClass]
public class When_executing_enum_to_union_type_with_file_scoped_namespaces : with_union_type_code_provider
{
	protected override string WithSource() => @"
namespace Test;

public enum Child
{
    One,
    Two
}    
";
}

[TestClass]
public class When_executing_enum_to_union_type_with_keyword_enum_and_member_names : with_union_type_code_provider
{
	protected override string WithSource() => @"
namespace Test;

public enum Operator
{
    Event,
    Int,
    Object
}  
";
}