using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using VerifyMSTest;

namespace Switchyard.CodeGeneration.Test;

public class VerifyCSharpSource : VerifyBase
{
	protected readonly List<PortableExecutableReference> AdditionalReferences = new();

	protected Task VerifyGenerationResult(string source)
	{
		var syntaxTree = CSharpSyntaxTree.ParseText(source);

		var assemblyDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
		var references = new[]
			{
				typeof(object),
				typeof(Enumerable)
			}.Select(t => MetadataReference.CreateFromFile(t.Assembly.Location))
			.Concat(new []
			{
				MetadataReference.CreateFromFile(Path.Combine(assemblyDirectory, "System.Runtime.dll")),
				MetadataReference.CreateFromFile(Path.Combine(assemblyDirectory, "System.Collections.dll")),
			})
			.Concat(AdditionalReferences);

		var compilation = CSharpCompilation.Create(
			assemblyName: "Tests",
			syntaxTrees: new[] { syntaxTree },
			references: references,
			options: new(OutputKind.DynamicallyLinkedLibrary));

		var diagnostics = compilation.GetDiagnostics();
		var errors = string.Join(Environment.NewLine, diagnostics
			.Where(d => d.Severity == DiagnosticSeverity.Error));
		errors.Should().BeNullOrEmpty($"Compilation failed: {compilation.SyntaxTrees.Last()}");

		return Verify(source)
			.UseDirectory("Snapshots");
	}
}