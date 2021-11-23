using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sample;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test
{
    [TestClass]
    public class CLASS1
    {
        [TestMethod]
        public void Play()
        {
            
        }
    }

    public abstract class Test
    {
        public static readonly Test Eins = new Eins_();
        public static readonly Test Zwei = new Zwei_();

        public class Eins_ : Test
        {
            public Eins_() : base(UnionCases.Eins)
            {
            }
        }

        public class Zwei_ : Test
        {
            public Zwei_() : base(UnionCases.Zwei)
            {
            }
        }

        internal enum UnionCases    {
        Eins,
        Zwei
    }

        internal UnionCases UnionCase { get; }
        Test(UnionCases unionCase) => UnionCase = unionCase;

        public override string ToString() => Enum.GetName(typeof(UnionCases), UnionCase) ?? UnionCase.ToString();
        bool Equals(Test other) => UnionCase == other.UnionCase;

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
if (ReferenceEquals(this, obj)) return true;
if (obj.GetType() != GetType()) return false;
return Equals((Test) obj);
        }

        public override int GetHashCode() => (int) UnionCase;
    }

    public static class TestExtension
    {
        public static T Match<T>(this Test test, Func<Test.Eins_, T>eins, Func<Test.Zwei_, T>zwei)
        {
            switch (test.UnionCase)
            {
                case Test.UnionCases.Eins:
                    return eins((Test.Eins_)test);
                case Test.UnionCases.Zwei:
                    return zwei((Test.Zwei_)test);
                default:
                    throw new ArgumentException($"Unknown type derived from Test: {test.GetType().Name}");
            }
        }

        public static async Task<T> Match<T>(this Test test, Func<Test.Eins_, Task<T>>eins, Func<Test.Zwei_, Task<T>>zwei)
        {
            switch (test.UnionCase)
            {
                case Test.UnionCases.Eins:
                    return await eins((Test.Eins_)test).ConfigureAwait(false);
                case Test.UnionCases.Zwei:
                    return await zwei((Test.Zwei_)test).ConfigureAwait(false);
                default:
                    throw new ArgumentException($"Unknown type derived from Test: {test.GetType().Name}");
            }
        }

        public static async Task<T> Match<T>(this Task<Test>test, Func<Test.Eins_, T>eins, Func<Test.Zwei_, T>zwei) => (await test.ConfigureAwait(false)).Match(eins,zwei);
        public static async Task<T> Match<T>(this Task<Test>test, Func<Test.Eins_, Task<T>>eins, Func<Test.Zwei_, Task<T>>zwei) => await (await test.ConfigureAwait(false)).Match(eins,zwei).ConfigureAwait(false);
    }
}
