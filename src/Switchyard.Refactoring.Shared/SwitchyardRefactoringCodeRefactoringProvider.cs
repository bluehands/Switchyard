using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FunicularSwitch;
using Switchyard.CodeGeneration;

namespace Switchyard.Refactoring
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(SwitchyardRefactoringCodeRefactoringProvider)), Shared]
    internal class SwitchyardRefactoringCodeRefactoringProvider : CodeRefactoringProvider
    {
        public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            // TODO: Replace the following code with your own analysis, generating a CodeAction for each refactoring to offer

            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            // Find the node at the selection.
            var node = root.FindToken(context.Span.Start);

            if (!EnumToClassAction.HasSuggestedActions(node))
                return;

            // For any type declaration node, create a code action to reverse the identifier text.
            var action = CodeAction.Create("Reverse type name", c => Task.FromResult(context.Document.Project.Solution));

            // Register this code action.
            context.RegisterRefactoring(action);
        }

        private async Task<Solution> ReverseTypeNameAsync(Document document, TypeDeclarationSyntax typeDecl, CancellationToken cancellationToken)
        {
            // Produce a reversed version of the type declaration's identifier token.
            var identifierToken = typeDecl.Identifier;
            var newName = new string(identifierToken.Text.ToCharArray().Reverse().ToArray());

            // Get the symbol representing the type to be renamed.
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl, cancellationToken);

            // Produce a new solution that has all references to that type renamed, including the declaration.
            var originalSolution = document.Project.Solution;
            var optionSet = originalSolution.Workspace.Options;
            var newSolution = await Renamer.RenameSymbolAsync(document.Project.Solution, typeSymbol, newName, optionSet, cancellationToken).ConfigureAwait(false);

            // Return the new solution with the now-uppercase type name.
            return newSolution;
        }
    }

    public static class EnumToClassAction
    {
        public static bool HasSuggestedActions(Option<SyntaxToken> range) =>
            GetEnumDeclarationIfInRange(range) != null;

        public static EnumDeclarationSyntax GetEnumDeclarationIfInRange(Option<SyntaxToken> token)
        {
            return token.Bind(t =>
            {
                if (t.Parent != null)
                {
                    foreach (var node in t.Parent.AncestorsAndSelf())
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
            }).GetValueOrDefault();
        }

    }
}
