using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FunicularSwitch;
using Graphviz4Net.Dot.AntlrParser;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Switchyard.CodeGeneration
{
    public class StateMachineCodeProvider
    {
        readonly Workspace m_Workspace;

        public StateMachineCodeProvider(Workspace workspace) => m_Workspace = workspace;

        public async Task GenerateStateMachine(Document document, string dotFileName, CancellationToken cancellationToken)
        {
            if (!TryParseDotGraph(dotFileName, out var model))
                return;

            var documentRoot = await GenerateStateMachineCode(document, model, cancellationToken).ConfigureAwait(false);

            m_Workspace.UpdateRoot(document, documentRoot);
        }

        static async Task<SyntaxNode> GenerateStateMachineCode(Document document, StateMachineModel model, CancellationToken cancellationToken)
        {
            var documentRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            documentRoot = AddBaseInterfaceIfNotExists(documentRoot, model.BaseInterfaceName);

            foreach (var vertex in model.VertexClasses)
            {
                documentRoot = AddVertexClassDeclarationIfNotExists(documentRoot, vertex, model.BaseInterfaceName);
            }

            documentRoot = AddBaseParameterInterfaceIfNotExists(documentRoot, model.ParameterInterfaceName);
            documentRoot = AddParameterTypeIfNotExists(documentRoot, model.OuterParameterClassName);

            documentRoot = GenerateStateClassesRewriter.UpdateStateClasses(documentRoot, model);

            documentRoot = AddOrUpdateStateEnum(documentRoot, model);
            documentRoot = AddOrUpdateTriggerEnum(documentRoot, model);

            documentRoot = UpdateTransitionMethods(documentRoot, model);

            documentRoot = AddOrUpdateTransitionResultClass(documentRoot, model);
            documentRoot = AddOrUpdateExtensionClass(documentRoot, model);
            return documentRoot;
        }

        static bool TryParseDotGraph(string dotFileName, out StateMachineModel model)
        {
            try
            {
                var graph = AntlrParserAdapter<string>.GetParser().Parse(File.ReadAllText(dotFileName));
                model = new StateMachineModel(dotFileName, graph);
                return true;
            }
            catch (Exception)
            {
                model = null;
                return false;
            }
        }

        static SyntaxNode AddOrUpdateTransitionResultClass(SyntaxNode documentRoot, StateMachineModel names)
        {
            documentRoot = documentRoot.AddOrUpdateClass(names.TransitionResultClassName, c => c.Public().Abstract().AddMembers(), c => c);

            var baseList = SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(names.TransitionResultClassName));

            documentRoot = documentRoot.AddOrUpdateClass(names.TransitionResultTransitionClassName, c => c.Public().AddBaseListTypes(baseList), c => c
                .AddPropertyIfNotExists(names.BaseInterfaceName, "Source", p => p.WithGetter())
                .AddPropertyIfNotExists(names.BaseInterfaceName, "Destination", p => p.WithGetter())
                .AddPropertyIfNotExists(names.ParameterInterfaceName, "Trigger", p => p.WithGetter())
                .WithConstructorFromGetOnlyProperties()
            );

            documentRoot = documentRoot.AddOrUpdateClass(names.TransitionResultInvalidTriggerClassName, c => c.Public().AddBaseListTypes(baseList), c => c
                .AddPropertyIfNotExists(names.BaseInterfaceName, "Source", p => p.WithGetter())
                .AddPropertyIfNotExists(names.ParameterInterfaceName, "Trigger", p => p.WithGetter())
                .WithConstructorFromGetOnlyProperties()
            );

            return documentRoot;
        }

        static SyntaxNode AddOrUpdateExtensionClass(SyntaxNode documentRoot, StateMachineModel names)
        {
            var applyMethod = GenerateApplyMethod(names);

            var doTransitionMethod = GenerateDoTransitionMethod(names);

            var classDeclaration = names.TryGetExtensionClass(documentRoot)
                .Match(ext => ext, () =>
                {
                    var extensionClass = SyntaxFactory.ClassDeclaration(names.ExtensionClassName)
                        .Public()
                        .Static();
                    documentRoot = documentRoot.AddMemberToNamespace(extensionClass);
                    return extensionClass;
                });

            classDeclaration = classDeclaration.AddOrUpdateMethod(m => m.Identifier.ToString() == StateMachineModel.ApplyMethodName && m.ParameterList.Parameters.Count == 2, applyMethod);
            classDeclaration = classDeclaration.AddOrUpdateMethod(m => m.Identifier.ToString() == StateMachineModel.DoTransitionMethodName && m.ParameterList.Parameters.Count == 2, doTransitionMethod);

            classDeclaration = classDeclaration
                .AddMatchMethods(
                    names.BaseInterfaceName,
                    names.BaseName.FirstToLower(),
                    $"{StateMachineModel.StatePropertyName}.{StateMachineModel.EnumPropertyName}",
                    names.VertexClasses.Select(v => new MatchMethods.DerivedType(v.ClassName, v.StateName.FirstToLower(),
                            $"{names.OuterStateClassName}.{StateMachineModel.NestedEnumTypeName}.{v.StateName}"))
                        .ToImmutableList())
                .AddMatchMethods(
                    names.ParameterInterfaceName, "parameter",
                    $"{StateMachineModel.TriggerPropertyName}.{StateMachineModel.EnumPropertyName}",
                    names.VertexClasses.SelectMany(v => v.Transitions
                            .Select(t => new MatchMethods.DerivedType(t.FullParameterClassName,
                                t.MethodName.FirstToLower(),
                                $"{names.OuterTriggerClassName}.{StateMachineModel.NestedEnumTypeName}.{t.MethodName}")))
                        .Distinct().ToImmutableList()
                );

            return documentRoot.ReplaceNode(names.TryGetExtensionClass(documentRoot).GetValueOrThrow(), classDeclaration);
        }

        static MethodDeclarationSyntax GenerateApplyMethod(StateMachineModel names)
        {
            var returnType = SyntaxFactory.ParseTypeName(names.BaseInterfaceName);

            return GenerateApplyMethod(
                methodName: StateMachineModel.ApplyMethodName,
                names: names,
                returnType: returnType,
                generateSwitchStatement: (vertex, transition, baseTypeParameterName, parameterParameterName) => SyntaxFactory.ParseStatement(
                    $"return (({vertex.ClassName}){baseTypeParameterName}).{transition.MethodName}(({transition.FullParameterClassName}){parameterParameterName});")
                , generateDefaultStatement: (vertex, baseTypeParamName, parameterParamName) => SyntaxFactory.ParseStatement($"return {baseTypeParamName};"));
        }

        static MethodDeclarationSyntax GenerateDoTransitionMethod(StateMachineModel names)
        {
            return GenerateApplyMethod(
                methodName: StateMachineModel.DoTransitionMethodName,
                names: names,
                returnType: SyntaxFactory.ParseTypeName(names.TransitionResultClassName),
                generateSwitchStatement: (vertex, transition, baseTypeParameterName, parameterParameterName) => SyntaxFactory.ParseStatement(
                    $"return new {names.TransitionResultTransitionClassName}({baseTypeParameterName}, (({vertex.ClassName}){baseTypeParameterName}).{transition.MethodName}(({transition.FullParameterClassName}){parameterParameterName}), {parameterParameterName});")
                , generateDefaultStatement: (vertex, baseTypeParamName, parameterParamName) => SyntaxFactory.ParseStatement($"return new {names.TransitionResultInvalidTriggerClassName}({baseTypeParamName}, {parameterParamName});"));
        }

        static MethodDeclarationSyntax GenerateApplyMethod(string methodName, StateMachineModel names, TypeSyntax returnType,
            Func<StateMachineModel.VertexClass, StateMachineModel.TransitionMethod, string, string, StatementSyntax> generateSwitchStatement, Func<StateMachineModel.VertexClass, string, string, StatementSyntax> generateDefaultStatement)
        {
            var baseTypeParameterName = names.BaseName.FirstToLower();
            var parameterParameterName = "parameter";
            var baseType = SyntaxFactory.ParseTypeName(names.BaseInterfaceName);

            var applyMethod = SyntaxFactory.MethodDeclaration(returnType, methodName)
                .Public()
                .Static()
                .WithParameterList(SyntaxFactory.ParameterList()
                    .AddParameters(
                        SyntaxFactory.Parameter(SyntaxFactory.ParseToken(baseTypeParameterName))
                            .WithModifiers(SyntaxTokenList.Create(SyntaxFactory.Token(SyntaxKind.ThisKeyword)))
                            .WithType(baseType),
                        SyntaxFactory.Parameter(SyntaxFactory.ParseToken(parameterParameterName))
                            .WithType(SyntaxFactory.ParseTypeName(names.ParameterInterfaceName))
                    )
                )
                .WithBody(SyntaxFactory.Block()
                    .AddStatements(
                        SyntaxFactory.SwitchStatement(SyntaxFactory.ParseExpression(
                                $"{baseTypeParameterName}.{StateMachineModel.StatePropertyName}.{StateMachineModel.EnumPropertyName}"))
                            .AddSections(names.VertexClasses.Select(vertex =>
                                    SyntaxFactory.SwitchSection()
                                        .AddLabels(SyntaxFactory.CaseSwitchLabel(SyntaxFactory.ParseExpression(
                                            $"{names.OuterStateClassName}.{StateMachineModel.NestedEnumTypeName}.{vertex.StateName}")))
                                        .AddStatements(SyntaxFactory.Block(
                                            SyntaxFactory
                                                .SwitchStatement(SyntaxFactory.ParseExpression(
                                                    $"{parameterParameterName}.{StateMachineModel.TriggerPropertyName}.{StateMachineModel.EnumPropertyName}"))
                                                .AddSections(vertex.Transitions.Select(transition =>
                                                        SyntaxFactory.SwitchSection()
                                                            .AddLabels(SyntaxFactory.CaseSwitchLabel(
                                                                SyntaxFactory.ParseExpression(
                                                                    $"{names.OuterTriggerClassName}.{StateMachineModel.NestedEnumTypeName}.{transition.MethodName}")))
                                                            .AddStatements(generateSwitchStatement(vertex, transition,
                                                                baseTypeParameterName, parameterParameterName))
                                                    )
                                                    .Concat(new[]
                                                    {
                                                        SyntaxFactory.SwitchSection()
                                                            .AddLabels(SyntaxFactory.DefaultSwitchLabel())
                                                            .AddStatements(generateDefaultStatement(vertex,
                                                                baseTypeParameterName, parameterParameterName))
                                                    })
                                                    .ToArray()
                                                )
                                        ))
                                )
                                .Concat(new[]
                                {
                                    SyntaxFactory.SwitchSection()
                                        .AddLabels(SyntaxFactory.DefaultSwitchLabel())
                                        .AddStatements(SyntaxFactory.ParseStatement(
                                            $"throw new ArgumentException($\"Unknown type implementing {names.BaseInterfaceName}: {{{baseTypeParameterName}.GetType().Name}}\");"))
                                })
                                .ToArray()
                            )
                    ));
            return applyMethod;
        }

        static SyntaxNode UpdateTransitionMethods(SyntaxNode documentRoot, StateMachineModel names)
        {
            var outerParameterClass = names.TryGetOuterParameterClass(documentRoot).GetValueOrThrow();

            foreach (var _ in names.VertexClasses
                .SelectMany(vertex => vertex.Transitions.Select(transition => new { vertex, transition })))
            {
                var parameterClass = outerParameterClass.TryGetFirstDescendant<ClassDeclarationSyntax>(n => n.Name() == _.transition.NestedParameterClassName).GetValueOrThrow();
                var parameterClassProperties = parameterClass.DescendantNodes()
                    .OfType<PropertyDeclarationSyntax>()
                    .Where(p => p.Type.Name() != names.OuterTriggerClassName)
                    .ToImmutableList();
                var vertexClass = _.vertex.TryGetVertexClass(documentRoot).GetValueOrThrow();

                var origOverloadWithFlattenedParams = _.transition.TryGetTransitionMethod(vertexClass, false);

                var overloadWithFlattenedParams = origOverloadWithFlattenedParams.GetValueOrDefault(() =>
                    SyntaxFactory.MethodDeclaration(
                        SyntaxFactory.ParseTypeName(_.transition.ReturnType), _.transition.MethodName)
                    .Public()
                    .WithBody(SyntaxFactory.Block()
                        .AddStatements(SyntaxFactory.ParseStatement($"return new {_.transition.ReturnType}();"))
                    ))
                    .WithParameterList(SyntaxFactory.ParameterList(new SeparatedSyntaxList<ParameterSyntax>().AddRange(
                        parameterClassProperties.Select(p =>
                            SyntaxFactory.Parameter(SyntaxFactory.ParseToken(p.Identifier.ToString().FirstToLower())).WithType(p.Type))
                        ))
                    );

                var newVertexClass = vertexClass.AddOrUpdateNode(origOverloadWithFlattenedParams, overloadWithFlattenedParams);

                var origOverloadWithParamClass = _.transition.TryGetTransitionMethod(newVertexClass, true);
                var overloadWithParamClass = origOverloadWithParamClass.GetValueOrDefault(() => SyntaxFactory.MethodDeclaration(
                        SyntaxFactory.ParseTypeName(_.transition.ReturnType), _.transition.MethodName)
                    .AddParameterListParameters(SyntaxFactory.Parameter(SyntaxFactory.ParseToken(StateMachineModel.ParametersParameterName))
                        .WithType(SyntaxFactory.ParseTypeName(_.transition.FullParameterClassName)))
                    .Public()
                    .WithBody(SyntaxFactory.Block())
                ).WithBody(SyntaxFactory.Block(
                    SyntaxFactory.ParseStatement($"return {_.transition.MethodName}({string.Join(",", parameterClassProperties.Select(p => $"{StateMachineModel.ParametersParameterName}.{p.Identifier.ToString()}"))});")
                ));
                newVertexClass = newVertexClass.AddOrUpdateNode(origOverloadWithParamClass, overloadWithParamClass);

                documentRoot = documentRoot.ReplaceNode(vertexClass, newVertexClass);
            }
            return documentRoot;
        }

        static SyntaxNode AddOrUpdateStateEnum(SyntaxNode documentRoot, StateMachineModel names)
        {
            var stateClassName = names.OuterStateClassName;
            var nestedEnumTypeName = StateMachineModel.NestedEnumTypeName;
            var enumMemberNames = names.VertexClasses.Select(v => v.StateName);

            return AddOrUpdateEnumClass(documentRoot, stateClassName, nestedEnumTypeName, enumMemberNames);
        }

        static SyntaxNode AddOrUpdateTriggerEnum(SyntaxNode documentRoot, StateMachineModel names)
        {
            var nestedEnumTypeName = StateMachineModel.NestedEnumTypeName;
            var enumMemberNames = names.VertexClasses.SelectMany(v => v.Transitions.Select(t => t.MethodName)).Distinct();

            return AddOrUpdateEnumClass(documentRoot, names.OuterTriggerClassName, nestedEnumTypeName, enumMemberNames);
        }

        static SyntaxNode AddOrUpdateEnumClass(SyntaxNode documentRoot, string stateClassName, string nestedEnumTypeName,
            IEnumerable<string> enumMemberNames)
        {
            var stateType = documentRoot.DescendantNodes().OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(n => n.Name() == stateClassName);
            var oldEnumDeclaration = stateType?.DescendantNodes().OfType<EnumDeclarationSyntax>()
                .FirstOrDefault(e => e.Name() == nestedEnumTypeName);


            if (oldEnumDeclaration == null)
            {
                var newEnumDeclaration = SyntaxFactory.EnumDeclaration(stateClassName)
                    .AddMembers(enumMemberNames.Select(SyntaxFactory.EnumMemberDeclaration).ToArray()).Public();
                documentRoot = documentRoot.AddMemberToNamespace(newEnumDeclaration);
                documentRoot = documentRoot.GenerateEnumClass(stateClassName, Option<ClassDeclarationSyntax>.None);
            }
            else
            {
                var newEnumDeclaration = oldEnumDeclaration
                    .WithMembers(SyntaxFactory.SeparatedList(
                        enumMemberNames.Select(SyntaxFactory.EnumMemberDeclaration).ToArray())
                    );
                documentRoot = documentRoot.ReplaceNode(oldEnumDeclaration, newEnumDeclaration)
                    .UpdateEnumClass(stateClassName);
            }

            return documentRoot;
        }

        static SyntaxNode AddBaseInterfaceIfNotExists(SyntaxNode documentRoot, string baseInterfaceName)
        {
            documentRoot = documentRoot.AddTypeDeclarationIfNotExists(baseInterfaceName, () =>
                SyntaxFactory
                    .InterfaceDeclaration(baseInterfaceName)
                    .Public());
            return documentRoot;
        }

        static SyntaxNode AddBaseParameterInterfaceIfNotExists(SyntaxNode documentRoot, string baseInterfaceName)
        {
            documentRoot = documentRoot.AddTypeDeclarationIfNotExists(baseInterfaceName, () =>
                SyntaxFactory
                    .InterfaceDeclaration(baseInterfaceName)
                    .Public());
            return documentRoot;
        }

        static SyntaxNode AddParameterTypeIfNotExists(SyntaxNode root, string parameterTypeName)
        {
            return root.AddTypeDeclarationIfNotExists(parameterTypeName,
                () => SyntaxFactory.ClassDeclaration(parameterTypeName)
                    .Public()
                    .Static());
        }

        static SyntaxNode AddVertexClassDeclarationIfNotExists(SyntaxNode documentRoot, StateMachineModel.VertexClass vertex, string baseInterfaceName)
        {
            return documentRoot.AddTypeDeclarationIfNotExists(vertex.ClassName,
                () => SyntaxFactory.ClassDeclaration(vertex.ClassName).Public()
                    .AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(baseInterfaceName))));
        }
    }

    public class GenerateStateClassesRewriter : CSharpSyntaxRewriter
    {
        readonly StateMachineModel m_Names;

        public GenerateStateClassesRewriter(StateMachineModel names) => m_Names = names;

        public static SyntaxNode UpdateStateClasses(SyntaxNode syntaxNode, StateMachineModel names)
        {
            var rewriter = new GenerateStateClassesRewriter(names);
            return rewriter.Visit(syntaxNode);
        }

        public override SyntaxNode VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            var nodeName = node.Name();
            if (nodeName == m_Names.BaseInterfaceName)
                return VisitBaseInterface(node);
            if (nodeName == m_Names.ParameterInterfaceName)
                return VisitParameterInterface(node);
            return base.VisitInterfaceDeclaration(node);
        }

        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            var nodeClass = node.Name();
            if (nodeClass == m_Names.OuterParameterClassName)
                return VisitParameterOuterClass(node);

            var vertex = m_Names.VertexClasses.FirstOrDefault(v => v.ClassName == nodeClass);
            if (vertex != null)
                return VisitVertexClass(node, vertex);

            return node;
        }

        SyntaxNode VisitParameterOuterClass(ClassDeclarationSyntax outerParameterClass)
        {
            outerParameterClass = AddNestedParameterClasses(outerParameterClass);

            return outerParameterClass;
        }

        ClassDeclarationSyntax AddNestedParameterClasses(ClassDeclarationSyntax outerParameterClass)
        {
            foreach (var parameterTypeName in m_Names.NestedParameterClassNames)
            {
                var nestedClass = outerParameterClass.FirstDescendantOrDefault<ClassDeclarationSyntax>(n => n.Identifier.ToString() == parameterTypeName);
                if (nestedClass == null)
                {
                    outerParameterClass = outerParameterClass.AddMembers(
                            SyntaxFactory.ClassDeclaration(parameterTypeName)
                                .AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(m_Names.ParameterInterfaceName)))
                                .Public()
                    );
                }
            }

            var newMembers = outerParameterClass.Members
                .Select(member =>
                {
                    if (!(member is ClassDeclarationSyntax nestedClass) || !m_Names.NestedParameterClassNames.Contains(nestedClass.Name()))
                    {
                        return member;
                    }

                    return nestedClass.AddPropertyIfNotExists(m_Names.OuterTriggerClassName,
                        StateMachineModel.TriggerPropertyName, p => p
                            .WithAccessorList(null)
                            .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(SyntaxFactory.ParseExpression($"{m_Names.OuterTriggerClassName}.{nestedClass.Name()}")))
                            .WithSemicolonToken(SyntaxFactory.ParseToken(";")));
                });

            return outerParameterClass
                .WithMembers(new SyntaxList<MemberDeclarationSyntax>(newMembers));
        }

        SyntaxNode VisitBaseInterface(InterfaceDeclarationSyntax node)
        {
            node = node.AddPropertyIfNotExists(m_Names.OuterStateClassName, StateMachineModel.StatePropertyName);
            return node;
        }

        SyntaxNode VisitParameterInterface(InterfaceDeclarationSyntax node)
        {
            node = node.AddPropertyIfNotExists(m_Names.OuterTriggerClassName, StateMachineModel.TriggerPropertyName);
            return node;
        }

        ClassDeclarationSyntax VisitVertexClass(ClassDeclarationSyntax node, StateMachineModel.VertexClass vertex)
        {
            if (node.BaseList?.Types.All(t => t.Type.Name() != m_Names.BaseInterfaceName) ?? true)
            {
                node = node.WithBaseList((node.BaseList ?? SyntaxFactory.BaseList()).AddTypes(
                    SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(m_Names.BaseInterfaceName))));
            }

            node = node.AddPropertyIfNotExists(m_Names.OuterStateClassName, StateMachineModel.StatePropertyName, p => p
                .WithAccessorList(null)
                .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(SyntaxFactory.ParseExpression($"{m_Names.OuterStateClassName}.{vertex.StateName}")))
                .WithSemicolonToken(SyntaxFactory.ParseToken(";")));

            return node;
        }
    }

    public static class FluentApiExtensions
    {
        public static Option<MethodDeclarationSyntax> TryGetTransitionMethod(this StateMachineModel.TransitionMethod t, SyntaxNode vertexClass, bool overloadWithParamsClass)
        {
            return vertexClass.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m =>
                {
                    var name = m.ParameterList.Parameters.FirstOrDefault()?.Type.Name();
                    return m.Identifier.ToString() == t.MethodName && (overloadWithParamsClass &&
                           name == t.FullParameterClassName || !overloadWithParamsClass && name != t.FullParameterClassName);
                }).ToOption();
        }

        public static Option<ClassDeclarationSyntax> TryGetVertexClass(this StateMachineModel.VertexClass vertex, SyntaxNode node)
        {
            return node.TryGetFirstDescendant<ClassDeclarationSyntax>(n => n.Name() == vertex.ClassName);
        }

        public static Option<ClassDeclarationSyntax> TryGetOuterParameterClass(this StateMachineModel names, SyntaxNode node)
        {
            return node.TryGetFirstDescendant<ClassDeclarationSyntax>(n => n.Name() == names.OuterParameterClassName);
        }

        public static Option<ClassDeclarationSyntax> TryGetExtensionClass(this StateMachineModel names, SyntaxNode node)
        {
            return node.TryGetFirstDescendant<ClassDeclarationSyntax>(n => n.Name() == names.ExtensionClassName);
        }
    }
}
