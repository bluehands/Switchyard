using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using FunicularSwitch.Extensions;
using Graphviz4Net.Dot;

namespace Switchyard.CodeGeneration
{
    public class StateMachineModel
    {
        public const string ParametersParameterName = "parameters";
        public const string NestedEnumTypeName = WrapEnumToClass.DefaultNestedEnumTypeName;
        public const string EnumPropertyName = WrapEnumToClass.DefaultEnumPropertyName;
        public const string ApplyMethodName = "Apply";
        public const string DoTransitionMethodName = "DoTransition";
        public const string MatchMethodName = "Match";

        public string BaseName { get; }
        //public string BaseInterfaceName { get; }
        public string StateClassName { get; }
        //public string OuterParameterClassName { get; }
        //public string ParameterInterfaceName { get; }
        public string TriggerClassName { get; }

        public ImmutableList<VertexClass> VertexClasses { get; }

        public IEnumerable<string> NestedParameterClassNames => VertexClasses
            .SelectMany(_ => _.Transitions.Select(t => t.NestedParameterClassName))
            .Distinct();

        public string ExtensionClassName { get; }
        public string TransitionResultClassName { get; }
        public string TransitionResultInvalidTriggerClassName { get; }
        public string TransitionResultTransitionClassName { get; }

        public StateMachineModel(string dotFilename, DotGraph<string> stateGraph)
        {
            BaseName = Names.GetBaseName(dotFilename);
            //BaseInterfaceName = Names.GetBaseInterfaceName(dotFilename);
            //ParameterInterfaceName = Names.GetParameterInterfaceName(dotFilename);
            StateClassName = Names.GetStateClassName(dotFilename);
            TriggerClassName = Names.GetTriggerClassName(dotFilename);
            VertexClasses = stateGraph.AllVertices.Select(v =>
            {
                var transitions = stateGraph.VerticesEdges.Where(e => e.Source == v)
                    .Select(edge => new TransitionMethod(
                        sourceState: Names.GetStateName(edge.Source),
                        methodName: Names.GetTransitionMethod(edge),
                        returnType: Names.GetVertexClassName(dotFilename, edge.Destination),
                        fullParameterClassName: Names.GetParameterType(dotFilename, edge),
                        nestedParameterClassName: Names.GetTriggerNestedType(edge)));
                return new VertexClass(Names.GetStateName(v), Names.GetVertexClassName(dotFilename, v), transitions.ToImmutableList());
            }).ToImmutableList();
            //OuterParameterClassName = Names.GetParameterOuterType(dotFilename);
            ExtensionClassName = Names.GetExtensionClassName(dotFilename);
            TransitionResultClassName = Names.GetTransitionResultClassName(dotFilename);
            TransitionResultTransitionClassName = Names.GetTransitionResultTransitionClassName(dotFilename);
            TransitionResultInvalidTriggerClassName = Names.GetTransitionResultInvalidTriggerClassName(dotFilename);
        }

        public class VertexClass
        {
            public string StateName { get; }
            public QualifiedTypeName ClassName { get; }
            public ImmutableList<TransitionMethod> Transitions { get; }

            public VertexClass(string stateName, QualifiedTypeName className, ImmutableList<TransitionMethod> transitions)
            {
                StateName = stateName;
                ClassName = className;
                Transitions = transitions;
            }
        }

        public class TransitionMethod
        {
            public string SourceState { get; }
            public string MethodName { get; }
            public QualifiedTypeName ReturnType { get; }
            public string FullParameterClassName { get; }
            public string NestedParameterClassName { get; }

            public TransitionMethod(string sourceState, string methodName, QualifiedTypeName returnType, string fullParameterClassName, string nestedParameterClassName)
            {
                SourceState = sourceState;
                MethodName = methodName;
                ReturnType = returnType;
                FullParameterClassName = fullParameterClassName;
                NestedParameterClassName = nestedParameterClassName;
            }
        }

        static class Names
        {
            public static string GetStateClassName(string dotFileName) => $"{GetBaseName(dotFileName)}State";

            public static string GetTriggerClassName(string dotFileName) => $"{GetBaseName(dotFileName)}Trigger";

            public static string GetBaseName(string dotFileName) => Path.GetFileNameWithoutExtension(dotFileName);

            public static QualifiedTypeName GetVertexClassName(string dotFileName, DotVertex<string> vertex) => new($"{vertex.Id}_", GetStateClassName(dotFileName).Yield());

            public static string GetTransitionMethod(DotEdge<string> edge) => edge.Label;

            public static string GetParameterType(string dotFileName, DotEdge<string> edge) => $"{GetTriggerClassName(dotFileName)}.{GetTriggerNestedType(edge)}";

            public static string GetTriggerNestedType(DotEdge<string> dotEdge) => $"{dotEdge.Label}_";

            public static string GetStateName(DotVertex<string> vertex) => vertex.Id;

            public static string GetExtensionClassName(string dotFilename) => $"{GetBaseName(dotFilename)}Extension";

            public static string GetTransitionResultClassName(string dotFilename) => $"{GetBaseName(dotFilename)}TransitionResult";

            public static string GetTransitionResultTransitionClassName(string dotFilename) => $"{GetBaseName(dotFilename)}Transition";

            public static string GetTransitionResultInvalidTriggerClassName(string dotFilename) => $"{GetBaseName(dotFilename)}InvalidTrigger";
        }
    }
}