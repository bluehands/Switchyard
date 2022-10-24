using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Switchyard.CodeGeneration.Test;

[TestClass]
public abstract class with_state_machine_code_provider : CodeProviderSpec
{
	protected override async Task Refactor(AdhocWorkspace workspace, Document document, SyntaxNode root)
	{
		var updatedDoc = await StateMachineCodeProvider.GenerateStateMachine(
				document, GetDotFile, CancellationToken.None)
			.ConfigureAwait(false);
		workspace.TryApplyChanges(updatedDoc.Project.Solution);
	}

	protected abstract (string dotFileName, string fileContent) GetDotFile();
}

[TestClass]
public class When_generating_state_machine_for_simple_dot_graph : with_state_machine_code_provider
{
	protected override string WithSource() => "namespace StateMachineSpecs {}";

	protected override (string dotFileName, string fileContent) GetDotFile() => ("License.dot", @"digraph LicenseFlow {
    
  NoLicense;
  NotRegistered;
  DemoRegistered;
  CommunityRegistered;    
  Full;
    
  NoLicense -> NotRegistered [label=""DemoLicenseFound""];
  NoLicense -> Full [label=""FullLicenseFound""];
  
  NotRegistered -> Full [label=""FullLicenseFound""];
  NotRegistered -> NotRegistered [label=""DemoLicenseFound""];
  NotRegistered -> DemoRegistered [label=""DemoRegistered""];
  
  DemoRegistered -> CommunityRegistered [label=""CommunityRegistered""];
  DemoRegistered -> DemoRegistered [label=""DemoLicenseFound""];
  DemoRegistered -> Full [label=""FullLicenseFound""];
  
  Full -> Full [label=""FullLicenseFound""];
  
  CommunityRegistered -> Full [label=""FullLicenseFound""];
  CommunityRegistered -> CommunityRegistered [label=""DemoLicenseFound""];
}");
}