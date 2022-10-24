using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FunicularSwitch;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using MoreLinq;

namespace Switchyard.CodeGeneration
{
    public static class ImmutableHelpersCodeProvider
    {
        public static Option<ClassDeclarationSyntax> TryGetClassDeclaration(SyntaxToken token)
        {
            if (token.Parent != null)
            {
                foreach (var node in token.Parent.AncestorsAndSelf())
                {
                    switch (node)
                    {
                        case ClassDeclarationSyntax classDeclaration:
                            return classDeclaration;
                    }
                }
            }

            return Option<ClassDeclarationSyntax>.None;
        }


        public static async Task<Document> GenerateWithExtension(Document document, ClassDeclarationSyntax classDeclaration, CancellationToken cancellationToken)
        {
	        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
	        if (root == null)
		        return document;

            var classInfos = TypeNameWalker.GetQualifiedTypeNames(root);

            var constructors = classDeclaration
                .Members
                .OfType<ConstructorDeclarationSyntax>()
                .ToImmutableArray();

            if (!constructors.Any())
                return document;

            var constructor = constructors
                .MaxBy(c => c.ParameterList.Parameters.Count)
                .First();


            var originalClassName = classDeclaration.Name();
            var classInfo = classInfos.First(c => c.Name == originalClassName);
            var fullClassName = classInfo.QualifiedName(".");
            var extensionClassName = $"{classInfo.QualifiedName("")}WithExtension";
            var extensionClass = SyntaxFactory
                .ClassDeclaration(extensionClassName)
                .Public()
                .Static()
                .AddMembers(
                    SyntaxFactory.MethodDeclaration(Types.String, "With")
                        .Public()
                        .Static()
                        .WithReturnType(SyntaxFactory.ParseTypeName(fullClassName))
                        .WithParameterList(ToWithParameters(fullClassName, classDeclaration, constructor.ParameterList))
                        .WithExpressionBody(Call(fullClassName, classDeclaration, constructor.ParameterList))

                ).NormalizeWhitespace();

            var updated = root.AddOrUpdateClass(extensionClassName, c => extensionClass, c => extensionClass);

            var updatedDoc = document.WithSyntaxRoot(updated);
            updatedDoc = await Formatter.FormatAsync(updatedDoc, cancellationToken: cancellationToken);
            return updatedDoc;
        }

        static string Call(string fullClassName, BaseTypeDeclarationSyntax classDeclaration,
            BaseParameterListSyntax constructorParameters)
        {
            var sb = new StringBuilder($"new {fullClassName}(");

            var thisParameterName = classDeclaration.Name().ToParameterName();

            var lines = constructorParameters.Parameters
                .Select(p =>
                {
                    var parameterName = p.Identifier.Text;
                    var propertyName = parameterName.FirstToUpper();

                    return $"{parameterName}: {parameterName} != null ? {parameterName}.Match(x => x, () => {thisParameterName}.{propertyName}) : {thisParameterName}.{propertyName}";
                });

            sb.AppendLine(lines.ToDelimitedString("," + Environment.NewLine));
            sb.AppendLine(")");

            return sb.ToString();
        }

        static ParameterListSyntax ToWithParameters(string fullClassName,
            BaseTypeDeclarationSyntax classDeclarationSyntax, BaseParameterListSyntax constructorParameterList)
        {
            var thisTypeName = fullClassName;
            var thisParameters = SyntaxFactory.Parameter(SyntaxFactory.Identifier(classDeclarationSyntax.Name().ToParameterName()))
                .WithType(SyntaxFactory.ParseTypeName(thisTypeName))
                .AddThis();

            var furtherParameters = constructorParameterList.Parameters
                .Select(p => p
                    .WithType(SyntaxFactory.ParseTypeName($"Option<{p.Type!.Name()}>{(CurrentCompilationOptions.Nullability ? "?" : "")}"))
                    .WithDefault(SyntaxFactory.EqualsValueClause(SyntaxFactory.ParseExpression("null")))
                );

            return SyntaxFactory.ParameterList()
                .AddParameters(
                    new[] { thisParameters }.Concat(furtherParameters).ToArray());

        }
    }
}