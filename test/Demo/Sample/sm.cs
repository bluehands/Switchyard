using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace Sample
{
    public abstract class MyResult
    {
        public enum Ids
        {
            Ok,
            Warning,
            Error
        }

        public Ids Id
        {
            get;
        }

        protected MyResult(Ids id)
        {
            Id = id;
        }

        public override string ToString()
        {
            return Enum.GetName(typeof(Ids), Id) ?? Id.ToString();
        }

        bool Equals(MyResult other)
        {
            return Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != GetType())
                return false;
            return Equals((MyResult)obj);
        }

        public override int GetHashCode()
        {
            return (int)Id;
        }

        public static readonly MyResult Ok = new Ok_();
        public static readonly MyResult Warning = new Warning_();
        public static readonly MyResult Error = new Error_();
        public class Ok_ : MyResult
        {
            public Ok_() : base(Ids.Ok)
            {
            }
        }

        public class Warning_ : MyResult
        {
            public Warning_() : base(Ids.Warning)
            {
            }
        }

        public class Error_ : MyResult
        {
            public Error_() : base(Ids.Error)
            {
            }
        }
    }


    public static class MyResultExtension
    {
        public static T Match<T>(this MyResult myResult, Func<MyResult.Ok_, T> ok, Func<MyResult.Warning_, T> warning, Func<MyResult.Error_, T> error)
        {
            switch (myResult.Id)
            {
                case MyResult.Ids.Ok:
                    return ok((MyResult.Ok_)myResult);
                case MyResult.Ids.Warning:
                    return warning((MyResult.Warning_)myResult);
                case MyResult.Ids.Error:
                    return error((MyResult.Error_)myResult);
                default:
                    throw new ArgumentException($"Unknown type implementing MyResult: {myResult.GetType().Name}");
            }
        }

        public static async Task<T> Match<T>(this MyResult myResult, Func<MyResult.Ok_, Task<T>> ok, Func<MyResult.Warning_, Task<T>> warning, Func<MyResult.Error_, Task<T>> error)
        {
            switch (myResult.Id)
            {
                case MyResult.Ids.Ok:
                    return await ok((MyResult.Ok_)myResult).ConfigureAwait(false);
                case MyResult.Ids.Warning:
                    return await warning((MyResult.Warning_)myResult).ConfigureAwait(false);
                case MyResult.Ids.Error:
                    return await error((MyResult.Error_)myResult).ConfigureAwait(false);
                default:
                    throw new ArgumentException($"Unknown type implementing MyResult: {myResult.GetType().Name}");
            }
        }

        public static async Task<T> Match<T>(this Task<MyResult> myResult, Func<MyResult.Ok_, T> ok, Func<MyResult.Warning_, T> warning, Func<MyResult.Error_, T> error)
        {
            return (await myResult.ConfigureAwait(false)).Match(ok, warning, error);
        }

        public static async Task<T> Match<T>(this Task<MyResult> myResult, Func<MyResult.Ok_, Task<T>> ok, Func<MyResult.Warning_, Task<T>> warning, Func<MyResult.Error_, Task<T>> error)
        {
            return await(await myResult.ConfigureAwait(false)).Match(ok, warning, error).ConfigureAwait(false);
        }
    }

    public interface Ism
    {
        smState State
        {
            get;
        }
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
        smTrigger Trigger
        {
            get;
        }
    }

    public static class smParameters
    {
        public class atob : IsmParameter
        {
            public smTrigger Trigger => smTrigger.atob;
        }
    }

    public abstract class smTrigger
    {
        public enum Ids
        {
            atob
        }

        public Ids Id
        {
            get;
        }

        protected smTrigger(Ids id)
        {
            Id = id;
        }

        public override string ToString()
        {
            return Enum.GetName(typeof(Ids), Id) ?? Id.ToString();
        }

        bool Equals(smTrigger other)
        {
            return Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != GetType())
                return false;
            return Equals((smTrigger)obj);
        }

        public override int GetHashCode()
        {
            return (int)Id;
        }

        public static readonly atob_ atob = new atob_();
        public class atob_ : smTrigger
        {
            public atob_() : base(Ids.atob)
            {
            }
        }
    }

    public abstract class smTransitionResult
    {
    }

    public class smTransition : smTransitionResult
    {
        public Ism Source
        {
            get;
        }

        public Ism Destination
        {
            get;
        }

        public IsmParameter Trigger
        {
            get;
        }

        public smTransition(Ism source, Ism destination, IsmParameter trigger)
        {
            Source = source;
            Destination = destination;
            Trigger = trigger;
        }
    }

    public class smInvalidTrigger : smTransitionResult
    {
        public Ism Source
        {
            get;
        }

        public IsmParameter Trigger
        {
            get;
        }

        public smInvalidTrigger(Ism source, IsmParameter trigger)
        {
            Source = source;
            Trigger = trigger;
        }
    }

    public static class smExtension
    {
        public static Ism Apply(this Ism sm, IsmParameter parameter)
        {
            switch (sm.State.Id)
            {
                case smState.Ids.a:
                    {
                        switch (parameter.Trigger.Id)
                        {
                            case smTrigger.Ids.atob:
                                return ((asm)sm).atob((smParameters.atob)parameter);
                            default:
                                return sm;
                        }
                    }

                case smState.Ids.b:
                    {
                        switch (parameter.Trigger.Id)
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
            switch (sm.State.Id)
            {
                case smState.Ids.a:
                    {
                        switch (parameter.Trigger.Id)
                        {
                            case smTrigger.Ids.atob:
                                return new smTransition(sm, ((asm)sm).atob((smParameters.atob)parameter), parameter);
                            default:
                                return new smInvalidTrigger(sm, parameter);
                        }
                    }

                case smState.Ids.b:
                    {
                        switch (parameter.Trigger.Id)
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
            switch (sm.State.Id)
            {
                case smState.Ids.a:
                    return a((asm)sm);
                case smState.Ids.b:
                    return b((bsm)sm);
                default:
                    throw new ArgumentException($"Unknown type implementing Ism: {sm.GetType().Name}");
            }
        }

        public static T Match<T>(this IsmParameter parameter, Func<smParameters.atob, T> atob)
        {
            switch (parameter.Trigger.Id)
            {
                case smTrigger.Ids.atob:
                    return atob((smParameters.atob)parameter);
                default:
                    throw new ArgumentException($"Unknown type implementing IsmParameter: {parameter.GetType().Name}");
            }
        }

        public static async Task<T> Match<T>(this Ism sm, Func<asm, Task<T>> a, Func<bsm, Task<T>> b)
        {
            switch (sm.State.Id)
            {
                case smState.Ids.a:
                    return await a((asm)sm).ConfigureAwait(false);
                case smState.Ids.b:
                    return await b((bsm)sm).ConfigureAwait(false);
                default:
                    throw new ArgumentException($"Unknown type implementing Ism: {sm.GetType().Name}");
            }
        }

        public static async Task<T> Match<T>(this Task<Ism> sm, Func<asm, T> a, Func<bsm, T> b)
        {
            return (await sm.ConfigureAwait(false)).Match(a, b);
        }

        public static async Task<T> Match<T>(this Task<Ism> sm, Func<asm, Task<T>> a, Func<bsm, Task<T>> b)
        {
            return await (await sm.ConfigureAwait(false)).Match(a, b).ConfigureAwait(false);
        }

        public static async Task<T> Match<T>(this IsmParameter parameter, Func<smParameters.atob, Task<T>> atob)
        {
            switch (parameter.Trigger.Id)
            {
                case smTrigger.Ids.atob:
                    return await atob((smParameters.atob)parameter).ConfigureAwait(false);
                default:
                    throw new ArgumentException($"Unknown type implementing IsmParameter: {parameter.GetType().Name}");
            }
        }

        public static async Task<T> Match<T>(this Task<IsmParameter> parameter, Func<smParameters.atob, T> atob)
        {
            return (await parameter.ConfigureAwait(false)).Match(atob);
        }

        public static async Task<T> Match<T>(this Task<IsmParameter> parameter, Func<smParameters.atob, Task<T>> atob)
        {
            return await (await parameter.ConfigureAwait(false)).Match(atob).ConfigureAwait(false);
        }
    }

    public abstract class smState
    {
        public enum Ids
        {
            a,
            b
        }

        public Ids Id
        {
            get;
        }

        protected smState(Ids id)
        {
            Id = id;
        }

        public override string ToString()
        {
            return Enum.GetName(typeof(Ids), Id) ?? Id.ToString();
        }

        bool Equals(smState other)
        {
            return Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != GetType())
                return false;
            return Equals((smState)obj);
        }

        public override int GetHashCode()
        {
            return (int)Id;
        }

        public static readonly a_ a = new a_();
        public static readonly b_ b = new b_();
        public class a_ : smState
        {
            public a_() : base(Ids.a)
            {
            }
        }

        public class b_ : smState
        {
            public b_() : base(Ids.b)
            {
            }
        }
    }
}