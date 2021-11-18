using System.Threading.Tasks;
using FunicularSwitch;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Switchyard
{
    public static class RoslynExtension
    {
        public static async Task<Option<INamedTypeSymbol>> TryGetTypeSymbol(this Solution solution, string fullTypeName)
        {
            INamedTypeSymbol type = null;
            foreach (var project in solution.Projects)
            {
                var compilation = await project.GetCompilationAsync().ConfigureAwait(false);
                type = compilation?.GetTypeByMetadataName(fullTypeName);
                if (type != null)
                    break;
            }
            return type.ToOption();
        }

        public static Option<T> TryFindParentNode<T>(this Location location) where T : SyntaxNode => TryFindParentNode<T>(GetSyntaxNode(location));

        public static Option<T> TryFindParentNode<T>(this SyntaxToken token) where T : SyntaxNode => TryFindParentNode<T>(token.Parent);

        public static SyntaxNode GetSyntaxNode(this Location location)
        {
            var node = location.SourceTree.GetRoot().FindToken(location.SourceSpan.Start).Parent;
            return node;
        }

        public static Option<T> TryFindParentNode<T>(this SyntaxNode node) where T : SyntaxNode
        {
            while (!(node is T))
            {
                if (node.Parent == null)
                {
                    return Option<T>.None;
                }

                node = node.Parent;
            }

            return (T)node;
        }

        public static InvocationExpressionSyntax ReplaceMethodName(this InvocationExpressionSyntax node, string originalMethodName,
            string newMethodName)
        {
            var logMethodAccess = (MemberAccessExpressionSyntax)node.Expression;
            if (logMethodAccess.Name.Identifier.Text == originalMethodName)
            {
                var newLogMethodAccess = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    logMethodAccess.Expression, SyntaxFactory.IdentifierName(newMethodName));

                return SyntaxFactory.InvocationExpression(newLogMethodAccess, node.ArgumentList);
            }

            return node;
        }
    }
}