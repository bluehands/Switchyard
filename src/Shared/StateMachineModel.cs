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
	    public string DotFilename { get; }
	    public const string TriggerParameterName = "trigger";
        public const string NestedEnumTypeName = WrapEnumToClass.DefaultNestedEnumTypeName;
        public const string EnumPropertyName = WrapEnumToClass.DefaultEnumPropertyName;
        public const string ApplyMethodName = "Apply";
        public const string DoTransitionMethodName = "DoTransition";
        public const string MatchMethodName = "Match";

        public string BaseName { get; }
        public string StateClassName { get; }
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
	        DotFilename = dotFilename;
	        BaseName = Names.GetBaseName(dotFilename);
            StateClassName = Names.GetStateClassName(dotFilename);
            TriggerClassName = Names.GetTriggerClassName(dotFilename);
            VertexClasses = stateGraph.AllVertices.Select(v =>
            {
                var transitions = stateGraph.VerticesEdges.Where(e => e.Source == v)
                    .Select(edge => new TransitionMethod(
                        sourceState: Names.GetStateName(edge.Source),
                        targetState: Names.GetStateName(edge.Destination),
                        trigger: edge.Label,
                        methodName: Names.GetTransitionMethod(edge),
                        returnType: Names.GetVertexClassName(dotFilename, edge.Destination),
                        fullParameterClassName: Names.GetParameterType(dotFilename, edge),
                        nestedParameterClassName: Names.GetTriggerNestedType(edge)));
                return new VertexClass(Names.GetStateName(v), Names.GetVertexClassName(dotFilename, v), transitions.ToImmutableList());
            }).ToImmutableList();
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
            public string TargetState { get; }
            public string MethodName { get; }
            public QualifiedTypeName ReturnType { get; }
            public string FullParameterClassName { get; }
            public string NestedParameterClassName { get; }
            public string Trigger { get; }

            public TransitionMethod(string sourceState, string targetState, string methodName,
	            QualifiedTypeName returnType, string fullParameterClassName, string nestedParameterClassName, string trigger)
            {
                SourceState = sourceState;
                TargetState = targetState;
                MethodName = methodName;
                ReturnType = returnType;
                FullParameterClassName = fullParameterClassName;
                NestedParameterClassName = nestedParameterClassName;
                Trigger = trigger;
            }
        }

        static class Names
        {
            public static string GetStateClassName(string dotFileName) => $"{GetBaseName(dotFileName)}State";

            public static string GetTriggerClassName(string dotFileName) => $"{GetBaseName(dotFileName)}Trigger";

            public static string GetBaseName(string dotFileName)
            {
	            static string RemoveAtEnd(string s, string toRemoveAtEnd) => s.EndsWith(toRemoveAtEnd) && s.Length > toRemoveAtEnd.Length ? s.Substring(0, s.Length - toRemoveAtEnd.Length) : s;

	            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(dotFileName);
	            fileNameWithoutExtension = RemoveAtEnd(fileNameWithoutExtension, "State");
	            fileNameWithoutExtension = RemoveAtEnd(fileNameWithoutExtension, "States");
	            return fileNameWithoutExtension;
            }

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