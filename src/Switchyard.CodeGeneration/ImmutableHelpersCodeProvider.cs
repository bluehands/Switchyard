using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MoreLinq;

namespace Switchyard.CodeGeneration
{
    public class ImmutableHelpersCodeProvider
    {
        readonly Workspace m_Workspace;

        public ImmutableHelpersCodeProvider(Workspace workspace) => m_Workspace = workspace;

        public async Task GenerateWithExtension(Document document, ClassDeclarationSyntax classDeclaration, CancellationToken cancellationToken)
        {
            var constructors = classDeclaration
                .Members
                .OfType<ConstructorDeclarationSyntax>()
                .ToImmutableArray();

            if (!constructors.Any())
                return;

            var constructor = constructors
                .MaxBy(c => c.ParameterList.Parameters.Count)
                .First();


            var originalClassName = classDeclaration.Name();
            var extensionClassName = $"{originalClassName}WithExtension";
            var extensionClass = SyntaxFactory
                .ClassDeclaration(extensionClassName)
                .Public()
                .Static()
                .AddMembers(
                    SyntaxFactory.MethodDeclaration(Types.String, "With")
                        .Public()
                        .Static()
                        .WithReturnType(SyntaxFactory.ParseTypeName(originalClassName))
                        .WithParameterList(ToWithParameters(classDeclaration, constructor.ParameterList))
                        .WithExpressionBody(Call(classDeclaration, constructor.ParameterList))

                ).NormalizeWhitespace();


            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var updated = root.AddOrUpdateClass(extensionClassName, c => extensionClass, c => extensionClass);

            m_Workspace.UpdateRoot(document, updated);
        }

        static string Call(BaseTypeDeclarationSyntax classDeclaration, BaseParameterListSyntax constructorParameters)
        {
            var sb = new StringBuilder($"new {classDeclaration.Name()}(");

            var thisParameterName = classDeclaration.Name().FirstToLower();

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

        static ParameterListSyntax ToWithParameters(BaseTypeDeclarationSyntax classDeclarationSyntax, BaseParameterListSyntax constructorParameterList)
        {
            var thisTypeName = classDeclarationSyntax.Name();
            var thisParameters = SyntaxFactory.Parameter(SyntaxFactory.Identifier(thisTypeName.FirstToLower()))
                .WithType(SyntaxFactory.ParseTypeName(thisTypeName))
                .AddThis();

            var furtherParameters = constructorParameterList.Parameters
                .Select(p => p
                    .WithType(SyntaxFactory.ParseTypeName($"Option<{p.Type.Name()}>"))
                    .WithDefault(SyntaxFactory.EqualsValueClause(SyntaxFactory.ParseExpression("null")))
                );

            return SyntaxFactory.ParameterList()
                .AddParameters(
                    new[] {thisParameters}.Concat(furtherParameters).ToArray());

        }
    }
}