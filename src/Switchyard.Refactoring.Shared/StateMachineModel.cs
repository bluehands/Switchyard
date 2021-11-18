using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Graphviz4Net.Dot;

namespace Switchyard.CodeGeneration
{
    public class StateMachineModel
    {
        public const string StatePropertyName = "State";
        public const string TriggerPropertyName = "Trigger";
        public const string ParametersParameterName = "parameters";
        public const string NestedEnumTypeName = WrapEnumToClass.DefaultNestedEnumTypeName;
        public const string EnumPropertyName = WrapEnumToClass.DefaultEnumPropertyName;
        public const string ApplyMethodName = "Apply";
        public const string DoTransitionMethodName = "DoTransition";
        public const string MatchMethodName = "Match";

        public string BaseName { get; }
        public string BaseInterfaceName { get; }
        public string OuterStateClassName { get; }
        public string OuterParameterClassName { get; }
        public string ParameterInterfaceName { get; }
        public string OuterTriggerClassName { get; }

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
            BaseInterfaceName = Names.GetBaseInterfaceName(dotFilename);
            ParameterInterfaceName = Names.GetParameterInterfaceName(dotFilename);
            OuterStateClassName = Names.GetStateClassName(dotFilename);
            OuterTriggerClassName = Names.GetTriggerClassName(dotFilename);
            VertexClasses = stateGraph.AllVertices.Select(v =>
            {
                var transitions = stateGraph.VerticesEdges.Where(e => e.Source == v)
                    .Select(edge => new TransitionMethod(
                        sourceState: Names.GetStateName(edge.Source),
                        methodName: Names.GetTransitionMethod(edge),
                        returnType: Names.GetVertexClassName(dotFilename, edge.Destination),
                        fullParameterClassName: Names.GetParameterType(dotFilename, edge),
                        nestedParameterClassName: Names.GetParameterNestedType(edge)));
                return new VertexClass(Names.GetStateName(v), Names.GetVertexClassName(dotFilename, v), transitions.ToImmutableList());
            }).ToImmutableList();
            OuterParameterClassName = Names.GetParameterOuterType(dotFilename);
            ExtensionClassName = Names.GetExtensionClassName(dotFilename);
            TransitionResultClassName = Names.GetTransitionResultClassName(dotFilename);
            TransitionResultTransitionClassName = Names.GetTransitionResultTransitionClassName(dotFilename);
            TransitionResultInvalidTriggerClassName = Names.GetTransitionResultInvalidTriggerClassName(dotFilename);
        }

        public class VertexClass
        {
            public string StateName { get; }
            public string ClassName { get; }
            public ImmutableList<TransitionMethod> Transitions { get; }

            public VertexClass(string stateName, string className, ImmutableList<TransitionMethod> transitions)
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
            public string ReturnType { get; }
            public string FullParameterClassName { get; }
            public string NestedParameterClassName { get; }

            public TransitionMethod(string sourceState, string methodName, string returnType, string fullParameterClassName, string nestedParameterClassName)
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

            public static string GetBaseInterfaceName(string dotFileName) => $"I{GetBaseName(dotFileName)}";

            public static string GetParameterInterfaceName(string dotFileName) => $"I{GetBaseName(dotFileName)}Parameter";

            public static string GetBaseName(string dotFileName) => Path.GetFileNameWithoutExtension(dotFileName);

            public static string GetVertexClassName(string dotFileName, DotVertex<string> vertex) => $"{vertex.Id}{GetBaseName(dotFileName)}";

            public static string GetTransitionMethod(DotEdge<string> edge) => edge.Label;

            public static string GetParameterType(string dotFileName, DotEdge<string> edge) => $"{GetBaseName(dotFileName)}Parameters.{GetParameterNestedType(edge)}";

            public static string GetParameterOuterType(string dotFileName) => $"{GetBaseName(dotFileName)}Parameters";

            public static string GetParameterNestedType(DotEdge<string> dotEdge) => dotEdge.Label;

            public static string GetStateName(DotVertex<string> vertex) => vertex.Id;

            public static string GetExtensionClassName(string dotFilename) => $"{GetBaseName(dotFilename)}Extension";

            public static string GetTransitionResultClassName(string dotFilename) => $"{GetBaseName(dotFilename)}TransitionResult";

            public static string GetTransitionResultTransitionClassName(string dotFilename) => $"{GetBaseName(dotFilename)}Transition";

            public static string GetTransitionResultInvalidTriggerClassName(string dotFilename) => $"{GetBaseName(dotFilename)}InvalidTrigger";
        }
    }
}