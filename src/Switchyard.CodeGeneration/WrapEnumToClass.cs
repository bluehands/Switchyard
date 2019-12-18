using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Switchyard.CodeGeneration
{
    public static class WrapEnumToClass
    {
        public const string DefaultNestedEnumTypeName = "UnionCases";
        public const string DefaultEnumPropertyName = "UnionCase";

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
                    .AddMembers(node
                        .WithIdentifier(SyntaxFactory.ParseToken(m_NestedEnumTypeName))
                        .Internal())
                    .AddProperty(m_NestedEnumTypeName, m_EnumPropertyName, p => p.Internal())
                    .AddMembers(SyntaxFactory.ConstructorDeclaration(node.Identifier.WithoutTrivia())
                        .AddParameterListParameters(SyntaxFactory.Parameter(SyntaxFactory.ParseToken(m_EnumPropertyName.FirstToLower()))
                            .WithType(SyntaxFactory.ParseTypeName(m_NestedEnumTypeName)))
                        .WithExpressionBody($"{m_EnumPropertyName} = {m_EnumPropertyName.FirstToLower()}")
                    )
                    .AddMembers(SyntaxFactory.MethodDeclaration(SyntaxFactory.ParseTypeName("string"), "ToString")
                        .Public()
                        .Override()
                        .WithExpressionBody($"Enum.GetName(typeof({m_NestedEnumTypeName}), {m_EnumPropertyName}) ?? {DefaultEnumPropertyName}.ToString()")
                    )
                    .AddMembers(SyntaxFactory.MethodDeclaration(SyntaxFactory.ParseTypeName("bool"), "Equals")
                        .AddParameterListParameters(SyntaxFactory.Parameter(SyntaxFactory.ParseToken("other")).WithType(SyntaxFactory.ParseTypeName(m_EnumTypeName)))
                        .WithExpressionBody($"{m_EnumPropertyName} == other.{m_EnumPropertyName}"))
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
                        .WithExpressionBody($"(int) {m_EnumPropertyName}")
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
                ClassDeclarationSyntax InsertAfterLastStaticCase(ClassDeclarationSyntax classDeclaration, IEnumerable<MemberDeclarationSyntax> toAdd)
                {
                    var members = classDeclaration.Members;
                    var staticCaseIndex = classDeclaration.Members.LastIndexOf(m =>
                        m.Modifiers.Select(_ => _.ToString()).Except(new[] {"readonly"}).SequenceEqual(new[] {"public", "static"}));
                    return classDeclaration.WithMembers(members.InsertRange(staticCaseIndex + 1, toAdd));
                }

                ClassDeclarationSyntax InsertAfterLastCaseDeclaration(ClassDeclarationSyntax classDeclaration, IEnumerable<MemberDeclarationSyntax> toAdd)
                {
                    var members = classDeclaration.Members;
                    var staticCaseIndex = classDeclaration.Members.LastIndexOf(m =>
                        m is ClassDeclarationSyntax dec && dec.BaseList?.Types.FirstOrDefault()?.Type.Name() == m_UnionTypeName);

                    return staticCaseIndex < 0
                        ? InsertAfterLastStaticCase(classDeclaration, toAdd)
                        : classDeclaration.WithMembers(members.InsertRange(staticCaseIndex + 1, toAdd));
                }

                if (node.Name() == m_UnionTypeName)
                {
                    var enumDeclaration = node.DescendantNodes().OfType<EnumDeclarationSyntax>().FirstOrDefault(n => n.Name() == m_NestedEnumTypeName);
                    
                    if (enumDeclaration != null)
                    {
                        var caseDeclarationMembers = enumDeclaration.Members.SelectMany(enumMember =>
                        {
                            // ReSharper disable once AccessToModifiedClosure
                            var existingCaseDeclaration = node.DescendantNodes().OfType<ClassDeclarationSyntax>()
                                .FirstOrDefault(n => n.Name() == GetEnumMemberClassName(enumMember.Identifier.ToString()));

                            if (existingCaseDeclaration == null)
                            {
                                var enumMemberClassName = GetEnumMemberClassName(enumMember.Identifier.ToString());

                                var caseDeclaration = SyntaxFactory.ClassDeclaration(enumMemberClassName)
                                    .Public()
                                    .AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(m_UnionTypeName)))
                                    .AddMembers(SyntaxFactory.ConstructorDeclaration(enumMemberClassName)
                                        .WithInitializer(SyntaxFactory.ConstructorInitializer(SyntaxKind.BaseConstructorInitializer)
                                            .AddArgumentListArguments(SyntaxFactory.Argument(SyntaxFactory.ParseExpression($"{m_NestedEnumTypeName}.{enumMember.Identifier}")))
                                        )
                                        .Public()
                                        .WithBody(SyntaxFactory.Block())
                                    );
                                return Option.Some(caseDeclaration);
                            }

                            return Option.None<ClassDeclarationSyntax>();
                        });

                        node = InsertAfterLastCaseDeclaration(node, caseDeclarationMembers);

                        var enumMemberNames = enumDeclaration.Members.Select(e => e.Identifier.ToString()).ToList();

                        var staticCaseMembers = enumMemberNames
                            .Select(enumMember =>
                        {
                            // ReSharper disable once AccessToModifiedClosure
                            var existingCaseDeclaration = node.DescendantNodes().OfType<ClassDeclarationSyntax>()
                                .First(n => n.Name() == GetEnumMemberClassName(enumMember));

                            var constructorParameters = existingCaseDeclaration
                                                             .Members.OfType<ConstructorDeclarationSyntax>()
                                                             .FirstOrDefault()?.ParameterList ?? SyntaxFactory.ParameterList();

                            if (constructorParameters.Parameters.Count > 0)
                            {
                                return (MemberDeclarationSyntax)SyntaxFactory.MethodDeclaration(
                                        SyntaxFactory.ParseTypeName(m_UnionTypeName),
                                        SyntaxFactory.ParseToken(enumMember))
                                    .WithParameterList(constructorParameters)
                                    .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(
                                        SyntaxFactory.ObjectCreationExpression(
                                            SyntaxFactory.ParseTypeName(GetEnumMemberClassName(enumMember)),
                                            SyntaxFactory.ArgumentList(
                                                new SeparatedSyntaxList<ArgumentSyntax>().AddRange(
                                                    constructorParameters.Parameters.Select(p =>
                                                        SyntaxFactory.Argument(SyntaxFactory.ParseExpression(p.Identifier.ToString())))))
                                            , null)))
                                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                                    .Public()
                                    .Static();
                            }

                            return SyntaxFactory.FieldDeclaration(
                                    SyntaxFactory.VariableDeclaration(SyntaxFactory.ParseTypeName(m_UnionTypeName))
                                        .AddVariables(SyntaxFactory
                                            .VariableDeclarator(SyntaxFactory.ParseToken(enumMember))
                                            .WithInitializer(SyntaxFactory.EqualsValueClause(
                                                SyntaxFactory.ObjectCreationExpression(
                                                    SyntaxFactory.ParseTypeName(GetEnumMemberClassName(enumMember)),
                                                    SyntaxFactory.ArgumentList(), null)))
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

                        node = node.WithMembers(node.Members.InsertRange(0, staticCaseMembers));

                        return node;
                    }
                }

                return base.VisitClassDeclaration(node);
            }

            static string GetEnumMemberClassName(string memberName) => $"{memberName}_";
        }
    }
}