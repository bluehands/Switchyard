using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Switchyard.CodeGeneration
{
    public static class MatchMethods
    {
        public class DerivedType
        {
            public string TypeName { get; }
            public string ParameterName { get; }
            public string EnumMember { get; }

            public DerivedType(string typeName, string parameterName, string enumMember)
            {
                TypeName = typeName;
                ParameterName = parameterName.IsAnyKeyWord() ? $"@{parameterName}" : parameterName;
                EnumMember = enumMember;
            }

            bool Equals(DerivedType other) => String.Equals(TypeName, other.TypeName) && String.Equals(ParameterName, other.ParameterName) && String.Equals(EnumMember, other.EnumMember);

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((DerivedType)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = TypeName.GetHashCode();
                    hashCode = (hashCode * 397) ^ ParameterName.GetHashCode();
                    hashCode = (hashCode * 397) ^ EnumMember.GetHashCode();
                    return hashCode;
                }
            }

            public static bool operator ==(DerivedType left, DerivedType right) => Equals(left, right);

            public static bool operator !=(DerivedType left, DerivedType right) => !Equals(left, right);
        }


        public static ClassDeclarationSyntax AddMatchMethods(this ClassDeclarationSyntax classDeclaration, string unionTypeName, ImmutableList<DerivedType> derivedTypes)
        {
            var baseTypeParameterName = unionTypeName.FirstToLower();
            var switchEnumMember = WrapEnumToClass.DefaultEnumPropertyName;

            return AddMatchMethods(classDeclaration, unionTypeName, baseTypeParameterName, switchEnumMember, derivedTypes);
        }

        public static ClassDeclarationSyntax AddMatchMethods(this ClassDeclarationSyntax classDeclaration, string unionTypeName,
            string baseTypeParameterName, string switchEnumMember, ImmutableList<DerivedType> derivedTypes)
        {
            return GenerateAll(unionTypeName, baseTypeParameterName, switchEnumMember, derivedTypes)
                .Aggregate(classDeclaration,
                    (dec, method) => dec.AddOrUpdateMethodMatchByFirstParameterType(method,
                        m => AssertSecondParameterHasSameType(method, m)));
        }

        static bool AssertSecondParameterHasSameType(BaseMethodDeclarationSyntax newMethod, BaseMethodDeclarationSyntax existing)
        {
            if (newMethod.ParameterList.Parameters.Count < 2) return true;
            return existing.ParameterList.Parameters.Count > 1 && existing.ParameterList.Parameters[1].Type.ToString() ==
                   newMethod.ParameterList.Parameters[1].Type.ToString();
        }

        public static IEnumerable<MethodDeclarationSyntax> GenerateAll(string baseTypeName,
            string baseTypeParameterName,
            string switchEnumMember, ImmutableList<DerivedType> derivedTypes)
        {
            yield return GenerateFuncSync(baseTypeName, baseTypeParameterName, switchEnumMember, derivedTypes);
            yield return GenerateFuncAsync(baseTypeName, baseTypeParameterName, switchEnumMember, derivedTypes);
            yield return GenerateFuncAsyncSync(baseTypeName, baseTypeParameterName, derivedTypes);
            yield return GenerateFuncAsyncAsync(baseTypeName, baseTypeParameterName, derivedTypes);
        }

        public static MethodDeclarationSyntax GenerateFuncSync(string baseTypeName, string baseTypeParameterName,
            string switchEnumMember, ImmutableList<DerivedType> derivedTypes)
        {
            return GenerateFuncSync(baseTypeName, baseTypeParameterName, switchEnumMember, derivedTypes, "T",
                d => SyntaxFactory.ParseStatement($"return {d.ParameterName}(({d.TypeName}){baseTypeParameterName});"));
        }

        public static MethodDeclarationSyntax GenerateFuncAsync(string baseTypeName, string baseTypeParameterName,
            string switchEnumMember, ImmutableList<DerivedType> derivedTypes)
        {
            return GenerateFuncSync(baseTypeName, baseTypeParameterName, switchEnumMember, derivedTypes, "Task<T>",
                    d => SyntaxFactory.ParseStatement($"return await {d.ParameterName}(({d.TypeName}){baseTypeParameterName}).ConfigureAwait(false);"))
                .Async();
        }

        public static MethodDeclarationSyntax GenerateFuncAsyncAsync(string baseTypeName, string baseTypeParameterName, ImmutableList<DerivedType> derivedTypes)
        {
            return MatchMethodDeclaration($"Task<{baseTypeName}>", baseTypeParameterName, derivedTypes, "Task<T>")
                .Async()
                .WithExpressionBody($"await (await {baseTypeParameterName}.ConfigureAwait(false)).Match({string.Join(",", derivedTypes.Select(d => d.ParameterName))}).ConfigureAwait(false)");
        }

        public static MethodDeclarationSyntax GenerateFuncAsyncSync(string baseTypeName, string baseTypeParameterName, ImmutableList<DerivedType> derivedTypes)
        {
            return MatchMethodDeclaration($"Task<{baseTypeName}>", baseTypeParameterName, derivedTypes, "Task<T>", "T")
                .Async()
                .WithExpressionBody($"(await {baseTypeParameterName}.ConfigureAwait(false)).Match({string.Join(",", derivedTypes.Select(d => d.ParameterName))})");
        }

        public static MethodDeclarationSyntax GenerateFuncSync(string baseTypeName, string baseTypeParameterName,
            string switchEnumMember,
            ImmutableList<DerivedType> derivedTypes, string returnType,
            Func<DerivedType, StatementSyntax> switchStatement)
        {
            var matchMethod = MatchMethodDeclaration(baseTypeName, baseTypeParameterName, derivedTypes, returnType)
                .WithBody(SyntaxFactory.Block()
                    .AddStatements(
                        SyntaxFactory.SwitchStatement(SyntaxFactory.ParseExpression($"{baseTypeParameterName}.{switchEnumMember}"))
                            .AddSections(derivedTypes.Select(derivedType =>
                                    SyntaxFactory.SwitchSection()
                                        .AddLabels(SyntaxFactory.CaseSwitchLabel(SyntaxFactory.ParseExpression($"{derivedType.EnumMember}")))
                                        .AddStatements(switchStatement(derivedType))
                                )
                                .Concat(new[]{SyntaxFactory.SwitchSection()
                                    .AddLabels(SyntaxFactory.DefaultSwitchLabel())
                                    .AddStatements(SyntaxFactory.ParseStatement($"throw new ArgumentException($\"Unknown type implementing {baseTypeName}: {{{baseTypeParameterName}.GetType().Name}}\");"))
                                })
                                .ToArray()
                            )
                    ));
            return matchMethod;
        }

        static MethodDeclarationSyntax MatchMethodDeclaration(string baseTypeName, string baseTypeParameterName, ImmutableList<DerivedType> derivedTypes, string returnType, string funcReturnType = null)
        {
            funcReturnType = funcReturnType ?? returnType;
            return SyntaxFactory.MethodDeclaration(SyntaxFactory.ParseTypeName(returnType), StateMachineModel.MatchMethodName)
                .WithTypeParameterList(SyntaxFactory.TypeParameterList().AddParameters(SyntaxFactory.TypeParameter("T")))
                .Public()
                .Static()
                .WithParameterList(SyntaxFactory.ParameterList()
                    .AddParameters(
                        SyntaxFactory.Parameter(SyntaxFactory.ParseToken(baseTypeParameterName))
                            .AddThis()
                            .WithType(SyntaxFactory.ParseTypeName(baseTypeName)))
                    .AddParameters(derivedTypes.Select(v =>
                        SyntaxFactory.Parameter(SyntaxFactory.ParseToken(v.ParameterName))
                            .WithType(SyntaxFactory.ParseTypeName($"Func<{v.TypeName}, {funcReturnType}>"))
                    ).ToArray())
                );
        }
    }
}