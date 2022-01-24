using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using System.Composition;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FunicularSwitch;
using Switchyard.CodeGeneration;

namespace Switchyard
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(SwitchyardRefactoringProvider)), Shared]
    internal class SwitchyardRefactoringProvider : CodeRefactoringProvider
    {
        public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            CurrentCompilationOptions.Nullability = context.Document.Project.CompilationOptions?.NullableContextOptions.Equals(NullableContextOptions.Enable) ?? true;

            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var node = root.FindToken(context.Span.Start);

            context.Register("Expand enum to union type", UnionTypeCodeProvider.TryGetEnumDeclaration,
                async (enumDeclaration, c) =>
                {
                    var updatedDoc = await UnionTypeCodeProvider.EnumToClass(context.Document, enumDeclaration, c);
                    return updatedDoc.Project.Solution;
                }, node);

            context.Register("Generate 'With' extension", ImmutableHelpersCodeProvider.TryGetClassDeclaration,
                async (classDeclaration, c) =>
                {
                    var updatedDoc = await ImmutableHelpersCodeProvider.GenerateWithExtension(context.Document, classDeclaration, c);
                    return updatedDoc.Project.Solution;
                }, node);

            context.Register(dotFilePath => $"Generate state machine from {Path.GetFileName(dotFilePath)}",
                _ => StateMachineCodeProvider.TryGetDotFilename(context.Document),
                async (dotFilePath, c) =>
                {
                    var updatedDoc = await StateMachineCodeProvider.GenerateStateMachine(context.Document, dotFilePath, c);
                    return updatedDoc.Project.Solution;
                }, node);
        }
    }

    public static class CodeRefactoringContextExtension
    {
        public static void Register<T>(this CodeRefactoringContext context, string title,
            Func<SyntaxToken, Option<T>> isRefactoringAvailable, Func<T, CancellationToken, Task<Solution>> codeAction,
            SyntaxToken node) => context.Register(_ => title, isRefactoringAvailable, codeAction, node);
        
        public static void Register<T>(this CodeRefactoringContext context, Func<T, string> title, Func<SyntaxToken, Option<T>> isRefactoringAvailable, Func<T, CancellationToken, Task<Solution>> codeAction, SyntaxToken node) =>
            isRefactoringAvailable(node).Match(scenario =>
            {
                var action = CodeAction.Create(title(scenario),
                    c => codeAction(scenario, c)
                );
                context.RegisterRefactoring(action);
            });

        public static void Register<T>(this CodeRefactoringContext context, string title, Func<SyntaxToken, Option<T>> isRefactoringAvailable, Func<T, CancellationToken, Task<Document>> codeAction, SyntaxToken node) =>
            isRefactoringAvailable(node).Match(scenario =>
            {
                var action = CodeAction.Create(title,
                    c => codeAction(scenario, c)
                );
                context.RegisterRefactoring(action);
            });
    }
}
