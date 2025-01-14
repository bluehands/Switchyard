using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FunicularSwitch;
using Graphviz4Net.Dot.AntlrParser;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Switchyard.CodeGeneration
{
    public static class StateMachineCodeProvider
	{
		public static Option<string> TryGetDotFilename(Document document)
		{
			var dotFilePath = Path.ChangeExtension(document.FilePath, "dot");
			var dotFileFound = File.Exists(dotFilePath);
			return dotFileFound ? dotFilePath : Option<string>.None;
		}

		public static async Task<Document> GenerateStateMachine(Document document, Func<(string dotFileName, string fileContent)> readDotNotation, CancellationToken cancellationToken)
		{
			if (!TryParseDotGraph(readDotNotation, out var model))
				return document;

			var documentRoot = await GenerateStateMachineCode(document, model, cancellationToken).ConfigureAwait(false);

			var updatedDoc = document.WithSyntaxRoot(documentRoot);
			updatedDoc = await Formatter.FormatAsync(updatedDoc, cancellationToken: cancellationToken);
			return updatedDoc;
		}

		static async Task<SyntaxNode> GenerateStateMachineCode(Document document, StateMachineModel model, CancellationToken cancellationToken)
		{
			var funicularGeneratorsReferenced = document.FunicularGeneratorsReferenced();

			var documentRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
			documentRoot = documentRoot!.AssertUsingDirectives("System");
			if (!funicularGeneratorsReferenced)
				documentRoot = documentRoot.AssertUsingDirectives("System.Threading.Tasks");

			documentRoot = AddSwitchyardComment(model.DotFilename, model.StateClassName, documentRoot);

			documentRoot = AddOrUpdateStateEnum(documentRoot, model, funicularGeneratorsReferenced);
			documentRoot = AddOrUpdateTriggerEnum(documentRoot, model, funicularGeneratorsReferenced);

			documentRoot = UpdateTransitionMethods(documentRoot, model);

			documentRoot = AddOrUpdateTransitionResultClass(documentRoot, model, funicularGeneratorsReferenced);
			documentRoot = AddOrUpdateExtensionClass(documentRoot, model, funicularGeneratorsReferenced);
			return documentRoot;
		}

		static SyntaxNode AddSwitchyardComment(string dotFileName, string stateClassName, SyntaxNode documentRoot)
		{
			var stateType = documentRoot.DescendantNodes().OfType<ClassDeclarationSyntax>()
				.FirstOrDefault(n => n.Name() == stateClassName);

			if (stateType == null)
			{
				var comment = Comment($"// State machine built with 'Switchyard' Visual Studio extension using 'Generate state machine from ...' refactoring based on {dotFileName}. See https://github.com/bluehands/Switchyard");

				return documentRoot.WithLeadingTrivia(documentRoot.GetLeadingTrivia().Add(comment).Add(CarriageReturnLineFeed));
			}

			return documentRoot;
		}

		static bool TryParseDotGraph(Func<(string dotFileName, string fileContent)> readDotNotation, out StateMachineModel model)
		{
			try
			{
				var (dotFileName, dotNotation) = readDotNotation();
				var graph = AntlrParserAdapter<string>.GetParser().Parse(dotNotation);
				model = new StateMachineModel(dotFileName, graph);
				return true;
			}
			catch (Exception)
			{
				model = null!;
				return false;
			}
		}

		static SyntaxNode AddOrUpdateTransitionResultClass(SyntaxNode documentRoot, StateMachineModel names,
			bool funicularGeneratorsReferenced)
		{
			documentRoot = documentRoot.AddOrUpdateClass(names.TransitionResultClassName, c => c.Public().Abstract().AddMembers(), c => funicularGeneratorsReferenced ? c.AssertUnionTypeAttribute() : c);

			var baseList = SimpleBaseType(ParseTypeName(names.TransitionResultClassName));

			documentRoot = documentRoot.AddOrUpdateClass(names.TransitionResultTransitionClassName, c => c.Public().AddBaseListTypes(baseList), c => c
				.AddPropertyIfNotExists(names.StateClassName, "Source", p => p.WithGetter())
				.AddPropertyIfNotExists(names.StateClassName, "Destination", p => p.WithGetter())
				.AddPropertyIfNotExists(names.TriggerClassName, "Trigger", p => p.WithGetter())
				.WithConstructorFromGetOnlyProperties()
			);

			documentRoot = documentRoot.AddOrUpdateClass(names.TransitionResultInvalidTriggerClassName, c => c.Public().AddBaseListTypes(baseList), c => c
				.AddPropertyIfNotExists(names.StateClassName, "Source", p => p.WithGetter())
				.AddPropertyIfNotExists(names.TriggerClassName, "Trigger", p => p.WithGetter())
				.WithConstructorFromGetOnlyProperties()
			);

			return documentRoot;
		}

		static SyntaxNode AddOrUpdateExtensionClass(SyntaxNode documentRoot, StateMachineModel names, bool funicularGeneratorsReferenced)
		{
			var applyMethod = GenerateApplyMethod(names);

			var doTransitionMethod = GenerateDoTransitionMethod(names);

			var classDeclaration = names.TryGetExtensionClass(documentRoot)
				.Match(ext => ext, () =>
				{
					var extensionClass = ClassDeclaration(names.ExtensionClassName)
						.Public()
						.Static();
					documentRoot = documentRoot.AddMemberToNamespace(extensionClass);
					return extensionClass;
				});

			classDeclaration = classDeclaration.AddOrUpdateMethod(m => m.Identifier.ToString() == StateMachineModel.ApplyMethodName && m.ParameterList.Parameters.Count == 2, applyMethod);
			classDeclaration = classDeclaration.AddOrUpdateMethod(m => m.Identifier.ToString() == StateMachineModel.DoTransitionMethodName && m.ParameterList.Parameters.Count == 2, doTransitionMethod);

			if (!funicularGeneratorsReferenced)
				classDeclaration = classDeclaration
					.AddMatchMethods(
						QualifiedTypeName.NoParents(names.StateClassName),
						names.BaseName.ToParameterName(),
						StateMachineModel.EnumPropertyName,
						names.VertexClasses.Select(v => new MatchMethods.DerivedType(v.ClassName.FullName, v.StateDisplayName.ToParameterName(),
								$"{names.StateClassName}.{StateMachineModel.NestedEnumTypeName}.{v.StateDisplayName}"))
							.ToImmutableList())
					.AddMatchMethods(
						QualifiedTypeName.NoParents(names.TriggerClassName),
						"trigger",
						StateMachineModel.EnumPropertyName,
						names.VertexClasses.SelectMany(v => v.Transitions
								.Select(t => new MatchMethods.DerivedType(t.FullParameterClassName,
									t.MethodName.ToParameterName(),
									$"{names.TriggerClassName}.{StateMachineModel.NestedEnumTypeName}.{t.MethodName}")))
							.Distinct().ToImmutableList()
					);

			return documentRoot.ReplaceNode(names.TryGetExtensionClass(documentRoot).GetValueOrThrow(), classDeclaration);
		}

		static MethodDeclarationSyntax GenerateApplyMethod(StateMachineModel names)
		{
			var returnType = ParseTypeName(names.StateClassName);

			return GenerateApplyMethod(
				methodName: StateMachineModel.ApplyMethodName,
				names: names,
				returnType: returnType,
				generateSwitchStatement: (vertex, transition, baseTypeParameterName, parameterParameterName) => ParseStatement(
					$"return (({vertex.ClassName}){baseTypeParameterName}).{transition.MethodName}(({transition.FullParameterClassName}){parameterParameterName});")
				, generateDefaultStatement: (vertex, baseTypeParamName, parameterParamName) => ParseStatement($"return {baseTypeParamName};"));
		}

		static MethodDeclarationSyntax GenerateDoTransitionMethod(StateMachineModel names)
		{
			return GenerateApplyMethod(
				methodName: StateMachineModel.DoTransitionMethodName,
				names: names,
				returnType: ParseTypeName(names.TransitionResultClassName),
				generateSwitchStatement: (vertex, transition, baseTypeParameterName, parameterParameterName) => ParseStatement(
					$"return new {names.TransitionResultTransitionClassName}({baseTypeParameterName}, (({vertex.ClassName}){baseTypeParameterName}).{transition.MethodName}(({transition.FullParameterClassName}){parameterParameterName}), {parameterParameterName});")
				, generateDefaultStatement: (_, baseTypeParamName, parameterParamName) => ParseStatement($"return new {names.TransitionResultInvalidTriggerClassName}({baseTypeParamName}, {parameterParamName});"));
		}

		static MethodDeclarationSyntax GenerateApplyMethod(string methodName, StateMachineModel names, TypeSyntax returnType,
			Func<StateMachineModel.VertexClass, StateMachineModel.TransitionMethod, string, string, StatementSyntax> generateSwitchStatement, Func<StateMachineModel.VertexClass, string, string, StatementSyntax> generateDefaultStatement)
		{
			var baseTypeParameterName = names.BaseName.ToParameterName();
			var parameterParameterName = "trigger";
			var baseType = ParseTypeName(names.StateClassName);

			var applyMethod = MethodDeclaration(returnType, methodName)
				.Public()
				.Static()
				.WithParameterList(ParameterList()
					.AddParameters(
						Parameter(ParseToken(baseTypeParameterName))
							.WithModifiers(SyntaxTokenList.Create(Token(SyntaxKind.ThisKeyword)))
							.WithType(baseType),
						Parameter(ParseToken(parameterParameterName))
							.WithType(ParseTypeName(names.TriggerClassName))
					)
				)
				.WithBody(Block()
					.AddStatements(
						SwitchStatement(ParseExpression(
								$"{baseTypeParameterName}.{StateMachineModel.EnumPropertyName}"))
							.AddSections(names.VertexClasses.Select(vertex =>
									SwitchSection()
										.AddLabels(CaseSwitchLabel(ParseExpression(
											$"{names.StateClassName}.{StateMachineModel.NestedEnumTypeName}.{vertex.StateDisplayName}")))
										.AddStatements(Block(
											SwitchStatement(ParseExpression(
													$"{parameterParameterName}.{StateMachineModel.EnumPropertyName}"))
												.AddSections(vertex.Transitions.Select(transition =>
														SwitchSection()
															.AddLabels(CaseSwitchLabel(
																ParseExpression(
																	$"{names.TriggerClassName}.{StateMachineModel.NestedEnumTypeName}.{transition.MethodName}")))
															.AddStatements(generateSwitchStatement(vertex, transition,
																baseTypeParameterName, parameterParameterName))
													)
													.Concat(new[]
													{
														SwitchSection()
															.AddLabels(DefaultSwitchLabel())
															.AddStatements(generateDefaultStatement(vertex,
																baseTypeParameterName, parameterParameterName))
													})
													.ToArray()
												)
										))
								)
								.Concat(new[]
								{
									SwitchSection()
										.AddLabels(DefaultSwitchLabel())
										.AddStatements(ParseStatement(
											$"throw new ArgumentException($\"Unknown type implementing {names.StateClassName}: {{{baseTypeParameterName}.GetType().Name}}\");"))
								})
								.ToArray()
							)
					));
			return applyMethod;
		}

		static SyntaxNode UpdateTransitionMethods(SyntaxNode documentRoot, StateMachineModel names)
		{
			var outerParameterClass = names.TryGetTriggerBaseClass(documentRoot).GetValueOrThrow();

			foreach (var _ in names.VertexClasses
				.SelectMany(vertex => vertex.Transitions.Select(transition => new { vertex, transition })))
			{
				var parameterClass = outerParameterClass.TryGetFirstDescendant<ClassDeclarationSyntax>(n => n.Name() == _.transition.NestedParameterClassName).GetValueOrThrow();
				var parameterClassProperties = parameterClass.DescendantNodes()
					.OfType<PropertyDeclarationSyntax>()
					.Where(p => p.Type.Name() != names.TriggerClassName)
					.ToImmutableList();
				var vertexClass = _.vertex.TryGetVertexClass(documentRoot).GetValueOrThrow();

				var origOverloadWithFlattenedParams = _.transition.TryGetTransitionMethod(vertexClass, false);

				var addEndRegionTrivia = origOverloadWithFlattenedParams.IsNone();
				var overloadWithFlattenedParams = origOverloadWithFlattenedParams.GetValueOrDefault(() =>
						MethodDeclaration(ParseTypeName(_.transition.ReturnType.Name),
								_.transition.MethodName)
							.Public()
							.WithExpressionBody($"new {_.transition.ReturnType.Name}()")
							.WithLeadingTrivia(Trivia(RegionDirectiveTrivia(true)
								.WithEndOfDirectiveToken(
									Token(
										TriviaList(
											PreprocessingMessage(
												$"{_.transition.SourceState} -> {_.transition.TargetState} [label=\"{_.transition.Trigger}\"]")),
										SyntaxKind.EndOfDirectiveToken,
										TriviaList()))))
							.NormalizeWhitespace())
					.WithParameterList(ParameterList(new SeparatedSyntaxList<ParameterSyntax>().AddRange(
							parameterClassProperties.Select(p =>
								Parameter(ParseToken(p.Identifier.ToString().ToParameterName()))
									.WithType(p.Type))
						))
					);

				var newVertexClass = vertexClass.AddOrUpdateNode(origOverloadWithFlattenedParams, overloadWithFlattenedParams);

				var origOverloadWithParamClass = _.transition.TryGetTransitionMethod(newVertexClass, true);
				var overloadWithParamClass = origOverloadWithParamClass.GetValueOrDefault(() => 
						MethodDeclaration(
							ParseTypeName(_.transition.ReturnType.Name), _.transition.MethodName)
						.AddParameterListParameters(Parameter(ParseToken(StateMachineModel.TriggerParameterName))
							.WithType(ParseTypeName(_.transition.FullParameterClassName)))
						.Public()
					)
					.WithBody(null)
					.WithExpressionBody(
						$"{_.transition.MethodName}({string.Join(",", parameterClassProperties.Select(p => $"{StateMachineModel.TriggerParameterName}.{p.Identifier}"))})");

				if (addEndRegionTrivia)
					overloadWithParamClass = overloadWithParamClass
						.WithLeadingTrivia(EndOfLine(Environment.NewLine))
						.WithTrailingTrivia(Trivia(
							EndRegionDirectiveTrivia(
								true)));

				newVertexClass = newVertexClass.AddOrUpdateNode(origOverloadWithParamClass, overloadWithParamClass);

				documentRoot = documentRoot.ReplaceNode(vertexClass, newVertexClass);
			}
			return documentRoot;
		}

		static SyntaxNode AddOrUpdateStateEnum(SyntaxNode documentRoot, StateMachineModel names,
			bool addUnionTypeAttribute)
		{
			var stateClassName = names.StateClassName;
			var nestedEnumTypeName = StateMachineModel.NestedEnumTypeName;
			var enumMemberNames = names.VertexClasses.Select(v => v.StateDisplayName);

			return AddOrUpdateEnumClass(documentRoot, stateClassName, nestedEnumTypeName, enumMemberNames, addUnionTypeAttribute);
		}

		static SyntaxNode AddOrUpdateTriggerEnum(SyntaxNode documentRoot, StateMachineModel names,
			bool addUnionTypeAttribute)
		{
			var nestedEnumTypeName = StateMachineModel.NestedEnumTypeName;
			var enumMemberNames = names.VertexClasses.SelectMany(v => v.Transitions.Select(t => t.MethodName)).Distinct();

			return AddOrUpdateEnumClass(documentRoot, names.TriggerClassName, nestedEnumTypeName, enumMemberNames, addUnionTypeAttribute);
		}

		static SyntaxNode AddOrUpdateEnumClass(SyntaxNode documentRoot,
			string stateClassName,
			string nestedEnumTypeName,
			IEnumerable<string> enumMemberNames, bool addUnionTypeAttribute)
		{
			var stateType = documentRoot.DescendantNodes().OfType<ClassDeclarationSyntax>()
				.FirstOrDefault(n => n.Name() == stateClassName);
			var oldEnumDeclaration = stateType?.DescendantNodes().OfType<EnumDeclarationSyntax>()
				.FirstOrDefault(e => e.Name() == nestedEnumTypeName);

			if (oldEnumDeclaration == null)
			{
				var newEnumDeclaration = EnumDeclaration(stateClassName)
					.AddMembers(enumMemberNames.Select(EnumMemberDeclaration).ToArray()).Public();
				documentRoot = documentRoot.AddMemberToNamespace(newEnumDeclaration);
				documentRoot = documentRoot.GenerateEnumClass(QualifiedTypeName.NoParents(stateClassName),
					Option<ClassDeclarationSyntax>.None, addUnionTypeAttribute);
			}
			else
			{
				var newEnumDeclaration = oldEnumDeclaration
					.WithMembers(SeparatedList(
						enumMemberNames.Select(EnumMemberDeclaration).ToArray())
					);
				documentRoot = documentRoot.ReplaceNode(oldEnumDeclaration, newEnumDeclaration)
					.UpdateEnumClass(QualifiedTypeName.NoParents(stateClassName), addUnionTypeAttribute);
			}

			return documentRoot;
		}
	}

	public static class FluentApiExtensions
	{
		public static Option<MethodDeclarationSyntax> TryGetTransitionMethod(this StateMachineModel.TransitionMethod t, SyntaxNode vertexClass, bool overloadWithParamsClass)
		{
			return vertexClass.DescendantNodes().OfType<MethodDeclarationSyntax>()
				.FirstOrDefault(m =>
				{
					var name = m.ParameterList.Parameters.FirstOrDefault()?.Type?.Name();
					return m.Identifier.ToString() == t.MethodName && (overloadWithParamsClass &&
						   name == t.FullParameterClassName || !overloadWithParamsClass && name != t.FullParameterClassName);
				}).ToOption();
		}

		public static Option<ClassDeclarationSyntax> TryGetVertexClass(this StateMachineModel.VertexClass vertex, SyntaxNode node)
		{
			return node.TryGetFirstDescendant<ClassDeclarationSyntax>(n => n.QualifiedName() == vertex.ClassName);
		}

		public static Option<ClassDeclarationSyntax> TryGetTriggerBaseClass(this StateMachineModel names, SyntaxNode node)
		{
			return node.TryGetFirstDescendant<ClassDeclarationSyntax>(n => n.Name() == names.TriggerClassName);
		}

		public static Option<ClassDeclarationSyntax> TryGetExtensionClass(this StateMachineModel names, SyntaxNode node)
		{
			return node.TryGetFirstDescendant<ClassDeclarationSyntax>(n => n.Name() == names.ExtensionClassName);
		}
	}
}
