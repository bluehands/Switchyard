using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Switchyard.CodeGeneration
{
    public static class TypeNameExtension
    {
        public static QualifiedTypeName QualifiedName(this BaseTypeDeclarationSyntax dec)
        {
            var current = dec.Parent as BaseTypeDeclarationSyntax;
            var typeNames = new Stack<string>();
            while (current != null)
            {
                typeNames.Push(current.Name());
                current = current.Parent as BaseTypeDeclarationSyntax;
            }

            return new QualifiedTypeName(dec.Name(), typeNames);
        }
    }

    public class TypeNameWalker : CSharpSyntaxWalker
    {
        readonly List<QualifiedTypeName> m_TypeNames = new List<QualifiedTypeName>();
        readonly Stack<string> m_ClassDeclarations = new Stack<string>();

        public static List<QualifiedTypeName> GetQualifiedTypeNames(SyntaxNode node)
        {
            var me = new TypeNameWalker();
            me.Visit(node);
            return me.m_TypeNames;
        }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            m_TypeNames.Add(new QualifiedTypeName(node.Name(), m_ClassDeclarations.Reverse()));
            m_ClassDeclarations.Push(node.Name());
            base.VisitClassDeclaration(node);
            m_ClassDeclarations.Pop();
        }

        public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
        {
            m_TypeNames.Add(new QualifiedTypeName(node.Name(), m_ClassDeclarations.Reverse()));
            base.VisitEnumDeclaration(node);
        }
    }

    public sealed class QualifiedTypeName : IEquatable<QualifiedTypeName>
    {
        public static QualifiedTypeName NoParents(string name) => new QualifiedTypeName(name, Enumerable.Empty<string>());

        public string FullName { get; }
        public ImmutableArray<string> NestingParents { get; }
        public string Name { get; }

        public string QualifiedName(string separator = ".") => NestingParents.Length > 0 ? $"{NestingParents.ToSeparatedString(separator)}{separator}{Name}" : Name;

        public QualifiedTypeName(string name, IEnumerable<string> nestingParents)
        {
            Name = name;
            NestingParents = nestingParents.ToImmutableArray();
            FullName = QualifiedName(".");
        }

        public override string ToString() => FullName;

        public bool Equals(QualifiedTypeName other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return FullName == other.FullName;
        }

        public override bool Equals(object obj) => ReferenceEquals(this, obj) || obj is QualifiedTypeName other && Equals(other);

        public override int GetHashCode() => FullName.GetHashCode();

        public static bool operator ==(QualifiedTypeName left, QualifiedTypeName right) => Equals(left, right);

        public static bool operator !=(QualifiedTypeName left, QualifiedTypeName right) => !Equals(left, right);
    }
}