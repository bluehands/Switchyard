using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FunicularSwitch;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Switchyard.CodeGeneration
{
    public class UnionTypeCodeProvider
    {
        readonly Workspace m_Workspace;

        public UnionTypeCodeProvider(Workspace workspace) => m_Workspace = workspace;

        public async Task EnumToClass(Document document, EnumDeclarationSyntax enumNode, CancellationToken cancellationToken)
        {
            var enumName = enumNode.QualifiedName();
            var caseTypeNames = enumNode.Members.Select(m => m.Identifier.Text);

            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            var unionType = enumName.Name == WrapEnumToClass.DefaultNestedEnumTypeName
                ? (ClassDeclarationSyntax) enumNode.Parent
                : Option.None<ClassDeclarationSyntax>();
            var unionTypeName = unionType.Match(u => u.QualifiedName(), () => enumNode.QualifiedName());

            root = root.GenerateEnumClass(enumName, unionType);

            var extensionClassName = $"{unionTypeName.QualifiedName("")}Extension";

            var classDeclaration = root
                .TryGetFirstDescendant<ClassDeclarationSyntax>(n => n.Name() == extensionClassName)
                .Match(ext => ext, () =>
                {
                    var extensionClass = SyntaxFactory.ClassDeclaration(extensionClassName)
                        .WithModifiers(unionType.Match(u => u.Modifiers, () => enumNode.Modifiers))
                        .Static();
                        
                    // ReSharper disable once AccessToModifiedClosure
                    root = root.AddMemberToNamespace(extensionClass, m => m is ClassDeclarationSyntax clazz && clazz.QualifiedName() == unionTypeName);
                    return extensionClass;
                });


            var derivedTypes = caseTypeNames.Select(n => new MatchMethods.DerivedType($"{unionTypeName}.{n}_", n.FirstToLower(), $"{unionTypeName}.{WrapEnumToClass.DefaultNestedEnumTypeName}.{n}")).ToImmutableList();

            classDeclaration = classDeclaration.AddMatchMethods(unionTypeName, derivedTypes);

            root = root.ReplaceNode(root.TryGetFirstDescendant<ClassDeclarationSyntax>(n => n.Name() == extensionClassName).GetValueOrThrow(), classDeclaration);

            m_Workspace.UpdateRoot(document, root);
        }
    }

    public static class UnionTypeModelExtension
    {
        public static UnionTypeModel ToUnionTypeModel(this EnumDeclarationSyntax enumDeclaration)
        {
            return new UnionTypeModel(
                name: enumDeclaration.Identifier.Text,
                subTypes: enumDeclaration.Members.Select(m => new UnionTypeModel.SubType(
                    name: m.Identifier.Text,
                    properties: ImmutableArray<UnionTypeModel.SubType.Property>.Empty
                )));
        }
    }

    public class UnionTypeOccurrence
    {
        public UnionTypeModel Model { get; }
        public Option<ClassDeclarationSyntax> AbstractBaseType { get; }
        public ImmutableArray<(UnionTypeModel.SubType SubType, Option<ClassDeclarationSyntax> SubTypeDelaraction)> SubTypes { get; }

        public UnionTypeOccurrence(UnionTypeModel model, Option<ClassDeclarationSyntax> abstractBaseType, IEnumerable<(UnionTypeModel.SubType SubType, Option<ClassDeclarationSyntax> SubTypeDelaraction)> subTypes)
        {
            Model = model;
            AbstractBaseType = abstractBaseType;
            SubTypes = subTypes.ToImmutableArray();
        }
    }

    public class UnionTypeModel
    {
        public string Name { get; }
        public ImmutableArray<SubType> SubTypes { get; }

        public UnionTypeModel(string name, IEnumerable<SubType> subTypes)
        {
            Name = name;
            SubTypes = subTypes.ToImmutableArray();
        }

        public class SubType
        {
            public string Name { get; }
            public ImmutableArray<Property> Properties { get; }

            public SubType(string name, ImmutableArray<Property> properties)
            {
                Properties = properties;
                Name = name;
            }

            public class Property
            {
                public string Name { get; }
                public string Type { get; }

                public Property(string name, string type)
                {
                    Name = name;
                    Type = type;
                }
            }
        }
    }

    public static class Types
    {
        public static TypeSyntax String = SyntaxFactory.ParseTypeName("string");
    }
}