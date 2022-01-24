using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FunicularSwitch;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace Switchyard.CodeGeneration
{
    public static class CurrentCompilationOptions
    {
        public static bool Nullability = false;
    }

    public static class UnionTypeCodeProvider
    {
        public static Option<EnumDeclarationSyntax> TryGetEnumDeclaration(SyntaxToken token)
        {
            if (token.Parent != null)
            {
                foreach (var node in token.Parent.AncestorsAndSelf())
                {
                    switch (node)
                    {
                        case EnumDeclarationSyntax enumDeclaration:
                            return enumDeclaration;

                        case ClassDeclarationSyntax classDeclaration:
                            {
                                var enumDeclaration = classDeclaration.Members.OfType<EnumDeclarationSyntax>()
                                    .FirstOrDefault(e => e.Name() == WrapEnumToClass.DefaultNestedEnumTypeName);

                                if (enumDeclaration != null)
                                    return enumDeclaration;
                                break;
                            }
                    }
                }
            }

            return Option<EnumDeclarationSyntax>.None;
        }

        public static async Task<Document> EnumToClass(Document document, EnumDeclarationSyntax enumNode, CancellationToken cancellationToken)
        {
            var funicularGeneratorsReferenced = document.FunicularGeneratorsReferenced();

            var enumName = enumNode.QualifiedName();
            var caseTypeNames = enumNode.Members.Select(m => m.Identifier.Text);

            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            var unionType = enumName.Name == WrapEnumToClass.DefaultNestedEnumTypeName
                ? (ClassDeclarationSyntax)enumNode.Parent
                : Option.None<ClassDeclarationSyntax>();
            var unionTypeName = unionType.Match(u => u.QualifiedName(), () => enumNode.QualifiedName());

            root = root.GenerateEnumClass(enumName, unionType, funicularGeneratorsReferenced);

            var extensionClassName = $"{unionTypeName.QualifiedName("")}Extension";

            if (!funicularGeneratorsReferenced)
                root = AddMatchExtensions(enumNode, root, extensionClassName, unionType, unionTypeName, caseTypeNames);

            document = document.WithSyntaxRoot(root);
            document = await Formatter.FormatAsync(document, cancellationToken: cancellationToken);
            return document;
        }

        static SyntaxNode AddMatchExtensions(EnumDeclarationSyntax enumNode, SyntaxNode root, string extensionClassName,
            Option<ClassDeclarationSyntax> unionType, QualifiedTypeName unionTypeName, IEnumerable<string> caseTypeNames)
        {
            var classDeclaration = root
                .TryGetFirstDescendant<ClassDeclarationSyntax>(n => n.Name() == extensionClassName)
                .Match(ext => ext, () =>
                {
                    var extensionClass = SyntaxFactory.ClassDeclaration(extensionClassName)
                        .WithModifiers(unionType.Match(u => u.Modifiers, () => enumNode.Modifiers))
                        .Static();

                    // ReSharper disable once AccessToModifiedClosure
                    root = root.AddMemberToNamespace(extensionClass,
                        m => m is ClassDeclarationSyntax clazz && clazz.QualifiedName() == unionTypeName);
                    return extensionClass;
                });


            var derivedTypes = caseTypeNames.Select(n => new MatchMethods.DerivedType($"{unionTypeName}.{n}_", n.FirstToLower(),
                $"{unionTypeName}.{WrapEnumToClass.DefaultNestedEnumTypeName}.{n}")).ToImmutableList();

            classDeclaration = classDeclaration.AddMatchMethods(unionTypeName, derivedTypes);

            var extClass = root.TryGetFirstDescendant<ClassDeclarationSyntax>(n => n.Name() == extensionClassName);
            root = root.ReplaceNode(extClass.GetValueOrThrow(), classDeclaration);
            return root;
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