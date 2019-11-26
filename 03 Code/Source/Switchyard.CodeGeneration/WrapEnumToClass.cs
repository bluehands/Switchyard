using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Switchyard.CodeGeneration
{
    public static class WrapEnumToClass
    {
        public const string DefaultNestedEnumTypeName = "Ids";
        public const string DefaultEnumPropertyName = "Id";

        public static SyntaxNode GenerateEnumClass(this SyntaxNode node, string enumTypeName, Option<ClassDeclarationSyntax> unionTypeDeclaration, string nestedEnumTypeName = DefaultNestedEnumTypeName, string enumPropertyName = DefaultEnumPropertyName)
        {
            var withEnumNested = unionTypeDeclaration.Match(u => node, () => new EnumToClassRewriter(enumTypeName, nestedEnumTypeName, enumPropertyName).Visit(node));
            return withEnumNested.UpdateEnumClass(unionTypeDeclaration.Match(u => u.Name(), () => enumTypeName));
        }

        public static SyntaxNode UpdateEnumClass(this SyntaxNode node, string unionTypeName, string enumPropertyName = DefaultNestedEnumTypeName) => 
            new AddEnumClassMembersRewriter(unionTypeName, enumPropertyName).Visit(node);

        public class EnumToClassRewriter : CSharpSyntaxRewriter
        {
            readonly string m_EnumTypeName;
            readonly string m_NestedEnumTypeName;
            readonly string m_EnumPropertyName;

            public EnumToClassRewriter(string enumTypeName, string nestedEnumTypeName = DefaultNestedEnumTypeName, string enumPropertyName = DefaultEnumPropertyName)
            {
                m_EnumTypeName = enumTypeName;
                m_NestedEnumTypeName = nestedEnumTypeName;
                m_EnumPropertyName = enumPropertyName;
            }

            public override SyntaxNode VisitEnumDeclaration(EnumDeclarationSyntax node)
            {
                var nodeName = node.Name();

                if (nodeName != m_EnumTypeName ) return base.VisitEnumDeclaration(node);

                var classDeclaration = SyntaxFactory.ClassDeclaration(m_EnumTypeName)
                    .WithModifiers(node.Modifiers)
                    .Abstract()
                    .AddMembers(node.WithIdentifier(SyntaxFactory.ParseToken(m_NestedEnumTypeName)).Public())
                    .AddProperty(m_NestedEnumTypeName, m_EnumPropertyName)
                    .AddMembers(SyntaxFactory.ConstructorDeclaration(node.Identifier)
                        .Protected()
                        .WithoutTrailingTrivia()
                        .AddParameterListParameters(SyntaxFactory.Parameter(SyntaxFactory.ParseToken(m_EnumPropertyName.FirstToLower()))
                            .WithType(SyntaxFactory.ParseTypeName(m_NestedEnumTypeName)))
                        .WithBody(SyntaxFactory.Block(SyntaxFactory.ParseStatement($"{m_EnumPropertyName} = {m_EnumPropertyName.FirstToLower()};")))
                    )
                    .AddMembers(SyntaxFactory.MethodDeclaration(SyntaxFactory.ParseTypeName("string"), "ToString")
                        .Public()
                        .Override()
                        .WithBody(SyntaxFactory.Block(SyntaxFactory.ParseStatement($"return Enum.GetName(typeof({m_NestedEnumTypeName}), {m_EnumPropertyName}) ?? Id.ToString();")))
                    )
                    .AddMembers(SyntaxFactory.MethodDeclaration(SyntaxFactory.ParseTypeName("bool"), "Equals")
                        .AddParameterListParameters(SyntaxFactory.Parameter(SyntaxFactory.ParseToken("other")).WithType(SyntaxFactory.ParseTypeName(m_EnumTypeName)))
                        .WithBody(SyntaxFactory.Block(SyntaxFactory.ParseStatement($"return {m_EnumPropertyName} == other.{m_EnumPropertyName};"))))
                    .AddMembers(SyntaxFactory.MethodDeclaration(SyntaxFactory.ParseTypeName("bool"), "Equals")
                        .Public()
                        .Override()
                        .AddParameterListParameters(SyntaxFactory.Parameter(SyntaxFactory.ParseToken("obj")).WithType(SyntaxFactory.ParseTypeName("object")))
                        .WithBody(SyntaxFactory.Block(
                            SyntaxFactory.ParseStatement("if (ReferenceEquals(null, obj)) return false;").WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed),
                            SyntaxFactory.ParseStatement("if (ReferenceEquals(this, obj)) return true;").WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed),
                            SyntaxFactory.ParseStatement("if (obj.GetType() != GetType()) return false;").WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed),
                            SyntaxFactory.ParseStatement($"return Equals(({m_EnumTypeName}) obj);")
                        )))
                    .AddMembers(SyntaxFactory.MethodDeclaration(SyntaxFactory.ParseTypeName("int"), "GetHashCode")
                        .Public()
                        .Override()
                        .WithParameterList(SyntaxFactory.ParameterList())
                        .WithBody(SyntaxFactory.Block(
                            SyntaxFactory.ParseStatement($"return (int) {m_EnumPropertyName};"))
                        )
                    );


                return classDeclaration;
            }
        }

        public class AddEnumClassMembersRewriter : CSharpSyntaxRewriter
        {
            readonly string m_UnionTypeName;
            readonly string m_NestedEnumTypeName;

            public AddEnumClassMembersRewriter(string unionTypeName, string nestedEnumTypeName = DefaultNestedEnumTypeName)
            {
                m_UnionTypeName = unionTypeName;
                m_NestedEnumTypeName = nestedEnumTypeName;
            }

            public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
            {
                if (node.Name() == m_UnionTypeName)
                {
                    var enumDeclaration = node.DescendantNodes().OfType<EnumDeclarationSyntax>().FirstOrDefault(n => n.Name() == m_NestedEnumTypeName);
                    
                    if (enumDeclaration != null)
                    {
                        var missingFields = enumDeclaration.Members.Select(e => e.Identifier.ToString())
                            .Except(node.DescendantNodes().OfType<FieldDeclarationSyntax>().Select(m => m.Declaration.Variables.FirstOrDefault()?.Identifier.ToString()));

                        foreach (var missingField in missingFields)
                        {
                            var staticField = SyntaxFactory.FieldDeclaration(
                                    SyntaxFactory.VariableDeclaration(SyntaxFactory.ParseTypeName(m_UnionTypeName))
                                        .AddVariables(SyntaxFactory.VariableDeclarator(SyntaxFactory.ParseToken(missingField))
                                            .WithInitializer(SyntaxFactory.EqualsValueClause(
                                                SyntaxFactory.ObjectCreationExpression(SyntaxFactory.ParseTypeName(GetEnumMemberClassName(missingField)), SyntaxFactory.ArgumentList(), null)))
                                        )
                                )
                                .Public()
                                .Static()
                                .ReadOnly();

                            var members = node.Members;
                            var index = node.Members.LastIndexOf(m =>
                                m is FieldDeclarationSyntax && staticField.Modifiers.All(modifier => m.Modifiers.Any(_ => _.ToString() == modifier.ToString())));
                            node = node.WithMembers(index >= 0
                                ? members.Insert(index + 1, staticField)
                                : members.Add(staticField));
                        }

                        foreach (var enumMember in enumDeclaration.Members)
                        {
                            var nestedClass = node.DescendantNodes().OfType<ClassDeclarationSyntax>()
                                .FirstOrDefault(n => n.Name() == GetEnumMemberClassName(enumMember.Identifier.ToString()));
                            if (nestedClass == null)
                            {
                                var enumMemberClassName = GetEnumMemberClassName(enumMember.Identifier.ToString());
                                node = node
                                    .AddMembers(SyntaxFactory.ClassDeclaration(enumMemberClassName)
                                        .WithModifiers(enumDeclaration.Modifiers)
                                        .AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(m_UnionTypeName)))
                                        .AddMembers(SyntaxFactory.ConstructorDeclaration(enumMemberClassName)
                                            .WithInitializer(SyntaxFactory.ConstructorInitializer(SyntaxKind.BaseConstructorInitializer)
                                                .AddArgumentListArguments(SyntaxFactory.Argument(SyntaxFactory.ParseExpression($"{m_NestedEnumTypeName}.{enumMember.Identifier}")))
                                            )
                                            .Public()
                                            .WithBody(SyntaxFactory.Block())
                                        )
                                    );
                            }
                        }

                        return node;
                    }
                }

                return base.VisitClassDeclaration(node);
            }

            static string GetEnumMemberClassName(string memberName) => $"{memberName}_";
        }
    }
}