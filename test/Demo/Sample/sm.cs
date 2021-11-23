using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace Sample;

public interface Ism
{
    smState State { get; }
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

    internal enum UnionCases
    {
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
        return Equals((Test)obj);
    }

    public override int GetHashCode() => (int)UnionCase;
}

public static class TestExtension
{
    public static T Match<T>(this Test test, Func<Test.Eins_, T> eins, Func<Test.Zwei_, T> zwei)
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

    public static async Task<T> Match<T>(this Test test, Func<Test.Eins_, Task<T>> eins, Func<Test.Zwei_, Task<T>> zwei)
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

    public static async Task<T> Match<T>(this Task<Test> test, Func<Test.Eins_, T> eins, Func<Test.Zwei_, T> zwei) => (await test.ConfigureAwait(false)).Match(eins, zwei);
    public static async Task<T> Match<T>(this Task<Test> test, Func<Test.Eins_, Task<T>> eins, Func<Test.Zwei_, Task<T>> zwei) => await(await test.ConfigureAwait(false)).Match(eins, zwei).ConfigureAwait(false);
}

public class asm : Ism
{
    public smState State => smState.a;

    public bsm atob()
    {
        return new bsm();
    }

    public bsm atob(smParameters.atob parameters)
    {
        return atob();
    }
}

public class bsm : Ism
{
    public smState State => smState.b;
}

public interface IsmParameter
{
    smTrigger Trigger { get; }
}

public static class smParameters
{
    public class atob : IsmParameter
    {
        public smTrigger Trigger => smTrigger.atob;
    }
}

public abstract class smState
{
    public static readonly smState a = new a_();
    public static readonly smState b = new b_();

    public class a_ : smState
    {
        public a_() : base(UnionCases.a)
        {
        }
    }

    public class b_ : smState
    {
        public b_() : base(UnionCases.b)
        {
        }
    }

    internal enum UnionCases
    {
        a,
        b
    }

    internal UnionCases UnionCase { get; }
    smState(UnionCases unionCase) => UnionCase = unionCase;

    public override string ToString() => Enum.GetName(typeof(UnionCases), UnionCase) ?? UnionCase.ToString();
    bool Equals(smState other) => UnionCase == other.UnionCase;

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((smState)obj);
    }

    public override int GetHashCode() => (int)UnionCase;
}

public abstract class smTrigger
{
    public static readonly smTrigger atob = new atob_();

    public class atob_ : smTrigger
    {
        public atob_() : base(UnionCases.atob)
        {
        }
    }

    internal enum UnionCases
    {
        atob
    }

    internal UnionCases UnionCase { get; }
    smTrigger(UnionCases unionCase) => UnionCase = unionCase;

    public override string ToString() => Enum.GetName(typeof(UnionCases), UnionCase) ?? UnionCase.ToString();
    bool Equals(smTrigger other) => UnionCase == other.UnionCase;

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((smTrigger)obj);
    }

    public override int GetHashCode() => (int)UnionCase;
}

public abstract class smTransitionResult
{
}

public class smTransition : smTransitionResult
{
    public Ism Source { get; }
    public Ism Destination { get; }
    public IsmParameter Trigger { get; }

    public smTransition(Ism source, Ism destination, IsmParameter trigger)
    {
        Source = source; Destination = destination; Trigger = trigger;
    }
}

public class smInvalidTrigger : smTransitionResult
{
    public Ism Source { get; }
    public IsmParameter Trigger { get; }

    public smInvalidTrigger(Ism source, IsmParameter trigger)
    {
        Source = source; Trigger = trigger;
    }
}

public static class smExtension
{
    public static Ism Apply(this Ism sm, IsmParameter parameter)
    {
        switch (sm.State.UnionCase)
        {
            case smState.UnionCases.a:
                {
                    switch (parameter.Trigger.UnionCase)
                    {
                        case smTrigger.UnionCases.atob:
                            return ((asm)sm).atob((smParameters.atob)parameter);
                        default:
                            return sm;
                    }
                }

            case smState.UnionCases.b:
                {
                    switch (parameter.Trigger.UnionCase)
                    {
                        default:
                            return sm;
                    }
                }

            default:
                throw new ArgumentException($"Unknown type implementing Ism: {sm.GetType().Name}");
        }
    }

    public static smTransitionResult DoTransition(this Ism sm, IsmParameter parameter)
    {
        switch (sm.State.UnionCase)
        {
            case smState.UnionCases.a:
                {
                    switch (parameter.Trigger.UnionCase)
                    {
                        case smTrigger.UnionCases.atob:
                            return new smTransition(sm, ((asm)sm).atob((smParameters.atob)parameter), parameter);
                        default:
                            return new smInvalidTrigger(sm, parameter);
                    }
                }

            case smState.UnionCases.b:
                {
                    switch (parameter.Trigger.UnionCase)
                    {
                        default:
                            return new smInvalidTrigger(sm, parameter);
                    }
                }

            default:
                throw new ArgumentException($"Unknown type implementing Ism: {sm.GetType().Name}");
        }
    }

    public static T Match<T>(this Ism sm, Func<asm, T> a, Func<bsm, T> b)
    {
        switch (sm.State.UnionCase)
        {
            case smState.UnionCases.a:
                return a((asm)sm);
            case smState.UnionCases.b:
                return b((bsm)sm);
            default:
                throw new ArgumentException($"Unknown type derived from Ism: {sm.GetType().Name}");
        }
    }

    public static async Task<T> Match<T>(this Ism sm, Func<asm, Task<T>> a, Func<bsm, Task<T>> b)
    {
        switch (sm.State.UnionCase)
        {
            case smState.UnionCases.a:
                return await a((asm)sm).ConfigureAwait(false);
            case smState.UnionCases.b:
                return await b((bsm)sm).ConfigureAwait(false);
            default:
                throw new ArgumentException($"Unknown type derived from Ism: {sm.GetType().Name}");
        }
    }

    public static async Task<T> Match<T>(this Task<Ism> sm, Func<asm, T> a, Func<bsm, T> b) => (await sm.ConfigureAwait(false)).Match(a, b);
    public static async Task<T> Match<T>(this Task<Ism> sm, Func<asm, Task<T>> a, Func<bsm, Task<T>> b) => await (await sm.ConfigureAwait(false)).Match(a, b).ConfigureAwait(false);

    public static T Match<T>(this IsmParameter parameter, Func<smParameters.atob, T> atob)
    {
        switch (parameter.Trigger.UnionCase)
        {
            case smTrigger.UnionCases.atob:
                return atob((smParameters.atob)parameter);
            default:
                throw new ArgumentException($"Unknown type derived from IsmParameter: {parameter.GetType().Name}");
        }
    }

    public static async Task<T> Match<T>(this IsmParameter parameter, Func<smParameters.atob, Task<T>> atob)
    {
        switch (parameter.Trigger.UnionCase)
        {
            case smTrigger.UnionCases.atob:
                return await atob((smParameters.atob)parameter).ConfigureAwait(false);
            default:
                throw new ArgumentException($"Unknown type derived from IsmParameter: {parameter.GetType().Name}");
        }
    }

    public static async Task<T> Match<T>(this Task<IsmParameter> parameter, Func<smParameters.atob, T> atob) => (await parameter.ConfigureAwait(false)).Match(atob);
    public static async Task<T> Match<T>(this Task<IsmParameter> parameter, Func<smParameters.atob, Task<T>> atob) => await (await parameter.ConfigureAwait(false)).Match(atob).ConfigureAwait(false);
}