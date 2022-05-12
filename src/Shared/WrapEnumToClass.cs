using System.Collections.Generic;
using System.Linq;
using FunicularSwitch;
using FunicularSwitch.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Switchyard.CodeGeneration
{
	public static class WrapEnumToClass
	{
		public const string DefaultNestedEnumTypeName = "UnionCases";
		public const string DefaultEnumPropertyName = "UnionCase";

		public static SyntaxNode GenerateEnumClass(this SyntaxNode node, QualifiedTypeName enumTypeName,
			Option<ClassDeclarationSyntax> unionTypeDeclaration, bool addUnionTypeAttribute,
			string nestedEnumTypeName = DefaultNestedEnumTypeName, string enumPropertyName = DefaultEnumPropertyName)
		{
			var withEnumNested = unionTypeDeclaration.Match(u => node, () => new EnumToClassRewriter(enumTypeName, nestedEnumTypeName, enumPropertyName).Visit(node));
			withEnumNested = withEnumNested.UpdateEnumClass(unionTypeDeclaration.Match(u => u.QualifiedName(), () => enumTypeName), addUnionTypeAttribute);
			if (node is CompilationUnitSyntax compilationUnitSyntax)
			{

			}
			return withEnumNested;
		}

		public static SyntaxNode UpdateEnumClass(this SyntaxNode node, QualifiedTypeName unionTypeName, bool addUnionTypeAttribute, string enumPropertyName = DefaultNestedEnumTypeName) =>
			new AddEnumClassMembersRewriter(unionTypeName, addUnionTypeAttribute, enumPropertyName).Visit(node);

		public class EnumToClassRewriter : CSharpSyntaxRewriter
		{
			readonly QualifiedTypeName m_EnumTypeName;
			readonly string m_NestedEnumTypeName;
			readonly string m_EnumPropertyName;

			public EnumToClassRewriter(QualifiedTypeName enumTypeName, string nestedEnumTypeName = DefaultNestedEnumTypeName, string enumPropertyName = DefaultEnumPropertyName)
			{
				m_EnumTypeName = enumTypeName;
				m_NestedEnumTypeName = nestedEnumTypeName;
				m_EnumPropertyName = enumPropertyName;
			}

			public override SyntaxNode VisitEnumDeclaration(EnumDeclarationSyntax node)
			{
				var nodeName = node.QualifiedName();

				if (nodeName != m_EnumTypeName) return base.VisitEnumDeclaration(node);

				var classDeclaration = ClassDeclaration(m_EnumTypeName.Name)
					.WithModifiers(node.Modifiers)
					.Abstract()
					.AddMembers(node
						.WithIdentifier(ParseToken(m_NestedEnumTypeName))
						.Internal())
					.AddProperty(m_NestedEnumTypeName, m_EnumPropertyName, p => p.Internal())
					.AddMembers(ConstructorDeclaration(node.Identifier.WithoutTrivia())
						.AddParameterListParameters(Parameter(ParseToken(m_EnumPropertyName.ToParameterName()))
							.WithType(ParseTypeName(m_NestedEnumTypeName)))
						.WithExpressionBody($"{m_EnumPropertyName} = {m_EnumPropertyName.ToParameterName()}")
					)
					.AddMembers(MethodDeclaration(ParseTypeName("string"), "ToString")
						.Public()
						.Override()
						.WithExpressionBody($"Enum.GetName(typeof({m_NestedEnumTypeName}), {m_EnumPropertyName}) ?? {DefaultEnumPropertyName}.ToString()")
					)
					.AddMembers(MethodDeclaration(ParseTypeName("bool"), "Equals")
						.AddParameterListParameters(Parameter(ParseToken("other")).WithType(ParseTypeName(m_EnumTypeName.Name)))
						.WithExpressionBody($"{m_EnumPropertyName} == other.{m_EnumPropertyName}"))
					.AddMembers(MethodDeclaration(ParseTypeName("bool"), "Equals")
						.Public()
						.Override()
						.AddParameterListParameters(Parameter(ParseToken("obj")).WithType(CurrentCompilationOptions.Nullability ? ParseTypeName("object?") : ParseTypeName("object")))
						.WithBody(Block(
							ParseStatement("if (ReferenceEquals(null, obj)) return false;").WithTrailingTrivia(CarriageReturnLineFeed),
							ParseStatement("if (ReferenceEquals(this, obj)) return true;").WithTrailingTrivia(CarriageReturnLineFeed),
							ParseStatement("if (obj.GetType() != GetType()) return false;").WithTrailingTrivia(CarriageReturnLineFeed),
							ParseStatement($"return Equals(({m_EnumTypeName.Name}) obj);")
						)))
					.AddMembers(MethodDeclaration(ParseTypeName("int"), "GetHashCode")
						.Public()
						.Override()
						.WithParameterList(ParameterList())
						.WithExpressionBody($"(int) {m_EnumPropertyName}")
					);

				return classDeclaration;
			}
		}

		public class AddEnumClassMembersRewriter : CSharpSyntaxRewriter
		{
			const string UnionTypeAttributeName = "FunicularSwitch.Generators.UnionType";
			readonly QualifiedTypeName m_UnionTypeName;
			readonly bool m_AddUnionTypeAttribute;
			readonly string m_NestedEnumTypeName;

			public AddEnumClassMembersRewriter(QualifiedTypeName unionTypeName, bool addUnionTypeAttribute,
				string nestedEnumTypeName = DefaultNestedEnumTypeName)
			{
				m_UnionTypeName = unionTypeName;
				m_AddUnionTypeAttribute = addUnionTypeAttribute;
				m_NestedEnumTypeName = nestedEnumTypeName;
			}

			public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
			{
				ClassDeclarationSyntax InsertAfterLastStaticMember(ClassDeclarationSyntax classDeclaration, IEnumerable<MemberDeclarationSyntax> toAdd)
				{
					var members = classDeclaration.Members;
					var staticCaseIndex = classDeclaration.Members.LastIndexOf(m =>
						m.Modifiers.Select(_ => _.ToString()).Except(new[] { "readonly" }).SequenceEqual(new[] { "public", "static" }));
					return classDeclaration.WithMembers(members.InsertRange(staticCaseIndex + 1, toAdd));
				}

				ClassDeclarationSyntax InsertCaseDeclaration(ClassDeclarationSyntax classDeclaration, IEnumerable<(ClassDeclarationSyntax caseDeclaration, int enumMemberIndex)> toAdd) =>
					toAdd.Aggregate(classDeclaration, (classDec, add) =>
					{
						var caseMembers = classDec.Members
							.Select((member, index) => (member, index))
							.Where(t =>
								t.member is ClassDeclarationSyntax dec &&
								dec.BaseList?.Types.FirstOrDefault()?.Type.Name() == m_UnionTypeName.Name)
							.ToList();

						var caseMemberIndexToInsertAfter = add.enumMemberIndex - 1;
						if (caseMembers.Count > caseMemberIndexToInsertAfter && caseMemberIndexToInsertAfter >= 0)
							return classDec.WithMembers(classDec.Members.Insert(caseMembers[caseMemberIndexToInsertAfter].index + 1, add.caseDeclaration));
						return InsertAfterLastStaticMember(classDeclaration, add.caseDeclaration.Yield());
					});

				if (node.QualifiedName() == m_UnionTypeName)
				{
					var enumDeclaration = node.DescendantNodes().OfType<EnumDeclarationSyntax>().FirstOrDefault(n => n.Name() == m_NestedEnumTypeName);

					if (enumDeclaration != null)
					{
						var caseDeclarationMembers = enumDeclaration.Members.SelectMany((enumMember, memberIndex) =>
						{
							// ReSharper disable once AccessToModifiedClosure
							var existingCaseDeclaration = node.DescendantNodes().OfType<ClassDeclarationSyntax>()
								.FirstOrDefault(n => n.Name() == GetEnumMemberClassName(enumMember.Identifier.ToString()));

							if (existingCaseDeclaration == null)
							{
								var enumMemberClassName = GetEnumMemberClassName(enumMember.Identifier.ToString());

								var caseDeclaration = ClassDeclaration(enumMemberClassName)
									.Public()
									.AddBaseListTypes(SimpleBaseType(ParseTypeName(m_UnionTypeName.Name)))
									.AddMembers(ConstructorDeclaration(enumMemberClassName)
										.WithInitializer(ConstructorInitializer(SyntaxKind.BaseConstructorInitializer)
											.AddArgumentListArguments(Argument(ParseExpression($"{m_NestedEnumTypeName}.{enumMember.Identifier}")))
										)
										.Public()
										.WithBody(Block())
									);
								return Option.Some((caseDeclaration, memberIndex));
							}

							return Option.None<(ClassDeclarationSyntax, int)>();
						});

						node = InsertCaseDeclaration(node, caseDeclarationMembers);

						var enumMemberNames = enumDeclaration.Members.Select(e => e.Identifier.ToString()).ToList();

						var staticCaseMembers = enumMemberNames
							.Select(enumMember =>
						{
							// ReSharper disable once AccessToModifiedClosure
							var existingCaseDeclaration = node.DescendantNodes().OfType<ClassDeclarationSyntax>()
								.First(n => n.Name() == GetEnumMemberClassName(enumMember));

							var constructorParameters = existingCaseDeclaration
															 .Members.OfType<ConstructorDeclarationSyntax>()
															 .FirstOrDefault()?.ParameterList ?? ParameterList();

							if (constructorParameters.Parameters.Count > 0)
							{
								return (MemberDeclarationSyntax)MethodDeclaration(
										ParseTypeName(m_UnionTypeName.Name),
										ParseToken(enumMember))
									.WithParameterList(constructorParameters)
									.WithExpressionBody(ArrowExpressionClause(
										ObjectCreationExpression(
											ParseTypeName(GetEnumMemberClassName(enumMember)),
											ArgumentList(
												new SeparatedSyntaxList<ArgumentSyntax>().AddRange(
													constructorParameters.Parameters.Select(p =>
														Argument(ParseExpression(p.Identifier.ToString())))))
											, null)))
									.WithSemicolonToken(Token(SyntaxKind.SemicolonToken))
									.Public()
									.Static();
							}

							return FieldDeclaration(
									VariableDeclaration(ParseTypeName(m_UnionTypeName.Name))
										.AddVariables(VariableDeclarator(ParseToken(enumMember))
											.WithInitializer(EqualsValueClause(
												ObjectCreationExpression(
													ParseTypeName(GetEnumMemberClassName(enumMember)),
													ArgumentList(), null)))
										)
								)
								.Public()
								.Static()
								.ReadOnly();
						});

						var existingStaticCaseNodes = node.Members
							.Where(m =>
							{
								var name = m is FieldDeclarationSyntax f
									? f.Declaration.Variables.FirstOrDefault()?.Identifier.ToString()
									: m is MethodDeclarationSyntax me
										? me.Name()
										: null;

								return name != null && enumMemberNames.Contains(name);
							});

						node = node.RemoveNodes(existingStaticCaseNodes, SyntaxRemoveOptions.KeepNoTrivia);

						// ReSharper disable once PossibleNullReferenceException
						node = node.WithMembers(node.Members.InsertRange(0, staticCaseMembers));

						node = AddUnionTypeAttribute(node);

						return node;
					}
				}

				return base.VisitClassDeclaration(node);
			}

			ClassDeclarationSyntax AddUnionTypeAttribute(ClassDeclarationSyntax node)
			{
				if (m_AddUnionTypeAttribute && node.AttributeLists
						.SelectMany(l => l.Attributes)
						.All(a => a.Name.ToString() != UnionTypeAttributeName))
				{
					node = node.WithAttributeLists(
						SingletonList(
							AttributeList(
								SingletonSeparatedList(
									Attribute(
											QualifiedName(
												QualifiedName(
													IdentifierName("FunicularSwitch"),
													IdentifierName("Generators")),
												IdentifierName("UnionType")))
										.WithArgumentList(
											AttributeArgumentList(
												SingletonSeparatedList(
													AttributeArgument(
															MemberAccessExpression(
																SyntaxKind.SimpleMemberAccessExpression,
																IdentifierName("CaseOrder"),
																IdentifierName("AsDeclared"))
															)
														.WithNameEquals(NameEquals(IdentifierName("CaseOrder")))))))))
					);
				}

				return node;
			}

			static string GetEnumMemberClassName(string memberName) => $"{memberName}_";
		}
	}
}