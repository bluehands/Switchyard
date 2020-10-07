using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Switchyard.CodeGeneration
{
    public class ClassInfoWalker : CSharpSyntaxWalker
    {
        readonly List<ClassInfo> m_ClassInfos = new List<ClassInfo>();
        readonly Stack<string> m_ClassDeclarations = new Stack<string>();

        public static List<ClassInfo> GetClassInfos(SyntaxNode node)
        {
            var me = new ClassInfoWalker();
            me.Visit(node);
            return me.m_ClassInfos;
        }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            m_ClassInfos.Add(new ClassInfo(node.Name(), m_ClassDeclarations.Reverse()));
            m_ClassDeclarations.Push(node.Name());
            base.VisitClassDeclaration(node);
            m_ClassDeclarations.Pop();
        }

        public class ClassInfo
        {
            public ImmutableArray<string> NestingParents { get; }
            public string Name { get; }

            public string QualifiedName(string separator) => NestingParents.Length > 0 ? $"{NestingParents.ToSeparatedString(separator)}{separator}{Name}" : Name;

            public ClassInfo(string name, IEnumerable<string> nestingParents)
            {
                Name = name;
                NestingParents = nestingParents.ToImmutableArray();
            }
        }
    }
}