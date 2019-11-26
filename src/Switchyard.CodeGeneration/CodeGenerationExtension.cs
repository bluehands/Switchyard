using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace Switchyard.CodeGeneration
{
    public static class CodeGenerationExtension
    {
        public static bool IsAnyKeyWord(this string identifier) =>
            SyntaxFacts.GetKeywordKind(identifier) != SyntaxKind.None
            || SyntaxFacts.GetContextualKeywordKind(identifier) != SyntaxKind.None;


        public static SyntaxNode AddMemberToNamespace(this SyntaxNode node, MemberDeclarationSyntax member, Func<MemberDeclarationSyntax, bool> afterMember = null)
        {
            var visitor = new AddMemberToNamespace(member, afterMember);
            return visitor.Visit(node);
        }

        public static T AssertModifier<T>(this T syntax, SyntaxKind modifier) where T : MemberDeclarationSyntax
        {
            var token = SyntaxFactory.Token(modifier);
            return (T) (syntax.Modifiers.Any(m => m.ToString() == token.ToString()) ? syntax : syntax.AddModifiers(token));
        }

        public static T Public<T>(this T syntax) where T : MemberDeclarationSyntax => syntax.AssertModifier(SyntaxKind.PublicKeyword);
        public static T Protected<T>(this T syntax) where T : MemberDeclarationSyntax => syntax.AssertModifier(SyntaxKind.ProtectedKeyword);
        public static T Static<T>(this T syntax) where T : MemberDeclarationSyntax => syntax.AssertModifier(SyntaxKind.StaticKeyword);

        public static T ReadOnly<T>(this T syntax) where T : MemberDeclarationSyntax => syntax.AssertModifier(SyntaxKind.ReadOnlyKeyword);
        public static T Abstract<T>(this T syntax) where T : MemberDeclarationSyntax => syntax.AssertModifier(SyntaxKind.AbstractKeyword);
        public static T Async<T>(this T syntax) where T : MemberDeclarationSyntax => syntax.AssertModifier(SyntaxKind.AsyncKeyword);
        public static T Override<T>(this T syntax) where T : MemberDeclarationSyntax => syntax.AssertModifier(SyntaxKind.OverrideKeyword);

        public static InterfaceDeclarationSyntax AddProperty(this InterfaceDeclarationSyntax node, string typeName,
            string identifier)
        {
            return node.AddMembers(MakeProperty(typeName, identifier, p => p.WithModifiers(new SyntaxTokenList())));
        }

        public static InterfaceDeclarationSyntax AddPropertyIfNotExists(this InterfaceDeclarationSyntax node,
            string typeName,
            string identifier)
        {
            var stateProperty = node.DescendantNodes().OfType<PropertyDeclarationSyntax>()
                .FirstOrDefault(p => p.Type.ToString() == typeName);
            if (stateProperty == null)
            {
                node = node.AddProperty(typeName, identifier);
            }

            return node;
        }

        public static ClassDeclarationSyntax AddProperty(this ClassDeclarationSyntax node, string typeName,
            string identifier,
            Func<PropertyDeclarationSyntax, PropertyDeclarationSyntax> mofify = null) =>
            node.AddMembers(MakeProperty(typeName, identifier, mofify));

        public static SyntaxNode AddTypeDeclarationIfNotExists(this SyntaxNode node, string className,
            Func<BaseTypeDeclarationSyntax> createType)
        {
            var declaration = TypeDeclarationFinder.TryGetTypeDeclaration(node, className);
            return declaration.Match(_ => node, () => node.AddMemberToNamespace(createType()));
        }

        public static ClassDeclarationSyntax AddPropertyIfNotExists(this ClassDeclarationSyntax node, string typeName,
            string identifier, Func<PropertyDeclarationSyntax, PropertyDeclarationSyntax> modify = null)
        {
            var stateProperty = node.DescendantNodes().OfType<PropertyDeclarationSyntax>()
                .FirstOrDefault(p => p.Type.ToString() == typeName && p.Name() == identifier);
            if (stateProperty == null)
            {
                node = node.AddProperty(typeName, identifier, modify);
            }

            return node;
        }

        public static ClassDeclarationSyntax WithConstructorFromGetOnlyProperties(this ClassDeclarationSyntax classDeclaration)
        {
            var properties = classDeclaration.Members.OfType<PropertyDeclarationSyntax>().ToImmutableList();
            var parameterList = SyntaxFactory.ParameterList(new SeparatedSyntaxList<ParameterSyntax>().AddRange(properties.Select(p => 
                SyntaxFactory.Parameter(SyntaxFactory.ParseToken(p.Name().FirstToLower())).WithType(p.Type))));

            if (classDeclaration.Members.OfType<ConstructorDeclarationSyntax>()
                .Any(c => c.ParameterList.Parameters.Count == parameterList.Parameters.Count && c.ParameterList.Parameters.Zip(parameterList.Parameters, (p1, p2) => p1.Type.Name() == p2.Type.Name()).All(b => b)))
            {
                return classDeclaration;
            }

            var body = SyntaxFactory.Block(
                properties.Select(p => SyntaxFactory.ParseStatement($"{p.Name()} = {p.Name().FirstToLower()};"))   
            );

            return classDeclaration
                .AddMembers(
                    SyntaxFactory.ConstructorDeclaration(classDeclaration.Name())
                        .Public()
                        .WithParameterList(parameterList)
                        .WithBody(body)
                );
        }

        public static PropertyDeclarationSyntax WithGetter(this PropertyDeclarationSyntax property) =>
            property.WithAccessorList(SyntaxFactory.AccessorList(
                new SyntaxList<AccessorDeclarationSyntax>(
                    SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                        .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)))));

        public static PropertyDeclarationSyntax MakeProperty(string typeName, string identifier,
            Func<PropertyDeclarationSyntax, PropertyDeclarationSyntax> modify = null)
        {
            var declarationSyntax = SyntaxFactory
                .PropertyDeclaration(SyntaxFactory.ParseTypeName(typeName), identifier)
                .AddAccessorListAccessors(SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithSemicolonToken(SyntaxFactory.ParseToken(";")))
                .Public();
            return modify != null ? modify(declarationSyntax) : declarationSyntax;
        }

        public static string Name(this BaseTypeDeclarationSyntax declaration) => declaration.Identifier.ToString();

        public static string Name(this MethodDeclarationSyntax declaration) => declaration.Identifier.ToString();

        public static string Name(this TypeSyntax typeSyntax) => typeSyntax.ToString();

        public static string Name(this PropertyDeclarationSyntax syntax) => syntax.Identifier.ToString();

        public static Option<T> TryGetFirstDescendant<T>(this SyntaxNode node, Func<T, bool> predicate) where T : class => FirstDescendantOrDefault(node, predicate).ToOption();

        public static T FirstDescendantOrDefault<T>(this SyntaxNode node, Func<T, bool> predicate) where T : class => node.DescendantNodes().OfType<T>().FirstOrDefault(predicate);

        public static ClassDeclarationSyntax AddOrUpdateMethodMatchByFirstParameterType(this ClassDeclarationSyntax classDeclaration, MethodDeclarationSyntax method, Func<MethodDeclarationSyntax, bool> additionalChecks = null)
        {
            additionalChecks = additionalChecks ?? (m => true);

            return AddOrUpdateMethod(classDeclaration, m =>
                m.Name() == method.Name() && m.ReturnType.ToString() == method.ReturnType.ToString() &&
                m.ParameterList.Parameters.FirstOrDefault()?.Type.Name() ==
                method.ParameterList.Parameters.FirstOrDefault()?.Type.Name() && additionalChecks(m), method);
        }

        public static ClassDeclarationSyntax AddOrUpdateMethod(this ClassDeclarationSyntax classDeclaration, Func<MethodDeclarationSyntax, bool> predicate,
            MethodDeclarationSyntax method)
        {
            classDeclaration = classDeclaration.AddOrUpdateNode(
                classDeclaration.Members.OfType<MethodDeclarationSyntax>().FirstOrDefault(predicate).ToOption(), method);
            return classDeclaration;
        }

        public static SyntaxNode ReplaceClass(this SyntaxNode root, Func<ClassDeclarationSyntax, bool> predicate, ClassDeclarationSyntax newClass) => root.ReplaceNode(root.FirstAncestorOrSelf(predicate), newClass);

        public static TRoot AddOrUpdateClass<TRoot>(this TRoot root, string className,
            Func<ClassDeclarationSyntax, ClassDeclarationSyntax> intialUpdateNode, Func<ClassDeclarationSyntax, ClassDeclarationSyntax> updateNode) where TRoot : SyntaxNode
        {
            return root.AddOrUpdateNode(c => c.Name() == className, () => intialUpdateNode(SyntaxFactory.ClassDeclaration(className)), updateNode);
        }

        public static TRoot AddOrUpdateNode<TRoot, TOrig>(this TRoot root, Func<TOrig, bool> predicate,
            Func<TOrig> createNode, Func<TOrig, MemberDeclarationSyntax> updateNode) where TOrig : SyntaxNode where TRoot : SyntaxNode
        {
            var orig = root.TryGetFirstDescendant(predicate)
                .GetValueOrDefault(createNode);

            var updated = updateNode(orig);

            return root.AddOrUpdateNode(root.TryGetFirstDescendant(predicate), updated);
        }

        public static TRoot AddOrUpdateNode<TRoot, T>(this TRoot root, Option<T> originalNode, MemberDeclarationSyntax newNode) where T : SyntaxNode where TRoot : SyntaxNode
        {
            return (TRoot)originalNode.Match(n => root.ReplaceNode(n, newNode), () =>
            {
                var subNodeToExpand = root.DescendantNodesAndSelf().Select(n =>
                {
                    var nameSpace = n as NamespaceDeclarationSyntax;
                    if (nameSpace != null)
                    {
                        return new NodeWithMembers(nameSpace, nameSpace.Members, (sn, i, m) =>
                        {
                            var ns = (NamespaceDeclarationSyntax)sn;
                            return ns.WithMembers(ns.Members.Insert(i, m));
                        });
                    }

                    var clazz = n as ClassDeclarationSyntax;
                    if (clazz != null)
                    {
                        return new NodeWithMembers(clazz, clazz.Members, (cn, i, m) =>
                        {
                            var ns = (ClassDeclarationSyntax)cn;
                            return ns.WithMembers(ns.Members.Insert(i, m));
                        });
                    }

                    return null;
                }).FirstOrDefault(_ => _ != null);

                if (subNodeToExpand != null)
                {
                    var lastMemberWithSameType = subNodeToExpand.Members.LastIndexOf(m => m.GetType() == newNode.GetType());
                    if (lastMemberWithSameType < 0) lastMemberWithSameType = subNodeToExpand.Members.Count;
                    else lastMemberWithSameType++;
                    var expanded = subNodeToExpand.InsertMember(subNodeToExpand.Node, lastMemberWithSameType, newNode);
                    if (ReferenceEquals(subNodeToExpand.Node, root))
                        return expanded;
                    return root.ReplaceNode(subNodeToExpand.Node, expanded);
                }

                return root;
            });
        }

        public static SyntaxNode Format(this SyntaxNode syntaxNode, Workspace workspace)
        {
            syntaxNode = Formatter.Format(syntaxNode.WithAdditionalAnnotations(Formatter.Annotation), Formatter.Annotation, workspace);
            return syntaxNode;
        }

        class NodeWithMembers
        {
            public SyntaxNode Node { get; }
            public SyntaxList<MemberDeclarationSyntax> Members { get; }
            public Func<SyntaxNode, int, MemberDeclarationSyntax, SyntaxNode> InsertMember { get; }


            public NodeWithMembers(SyntaxNode node, SyntaxList<MemberDeclarationSyntax> members, Func<SyntaxNode, int, MemberDeclarationSyntax, SyntaxNode> insertMember)
            {
                Node = node;
                Members = members;
                InsertMember = insertMember;
            }
        }
    }

    public class TypeDeclarationFinder : CSharpSyntaxWalker
    {
        readonly string m_TypeName;
        BaseTypeDeclarationSyntax m_Declaration;

        public static Option<BaseTypeDeclarationSyntax> TryGetTypeDeclaration(SyntaxNode node, string className)
        {
            var finder = new TypeDeclarationFinder(className);
            finder.Visit(node);
            return finder.m_Declaration.ToOption();
        }

        TypeDeclarationFinder(string typeName) => m_TypeName = typeName;

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            if (node.Identifier.ToString() == m_TypeName)
            {
                m_Declaration = node;
            }

            base.VisitClassDeclaration(node);
        }

        public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            if (node.Identifier.ToString() == m_TypeName)
            {
                m_Declaration = node;
            }

            base.VisitInterfaceDeclaration(node);
        }
    }

    public class AddMemberToNamespace : CSharpSyntaxRewriter
    {
        readonly MemberDeclarationSyntax m_Member;
        readonly Func<MemberDeclarationSyntax, bool> m_AfterMember;

        public AddMemberToNamespace(MemberDeclarationSyntax member, Func<MemberDeclarationSyntax, bool> afterMember = null)
        {
            m_Member = member;
            m_AfterMember = afterMember ?? (m => false);
        }

        public override SyntaxNode VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
        {
            var members = node.Members;
            var index = members.IndexOf(m_AfterMember);

            var newMembers = index >= 0 ? members.Insert(index + 1, m_Member) : members.Add(m_Member);

            return node.WithMembers(newMembers).NormalizeWhitespace();
        }
    }
}