using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Switchyard.CodeGeneration
{
    public class Option
    {
        public static Option<T> Some<T>(T value) => new Some<T>(value);

        public static Option<T> None<T>() => Option<T>.None;
    }

    public class Option<T> : Option, IEnumerable<T>
    {
#pragma warning disable CS0109 // Member does not hide an inherited member; new keyword is not required
        public new static readonly Option<T> None = new None<T>();
#pragma warning restore CS0109 // Member does not hide an inherited member; new keyword is not required

        public bool IsSome() => GetType() == typeof(Some<T>);

        public Option<T1> Convert<T1>(Func<T, T1> some)
        {
            return Match(t => Some(some(t)), None<T1>);
        }

        public Option<T1> Convert<T1>(Func<T, Option<T1>> some) => Match(some, None<T1>);

        public void Match(Action<T> some, Action none = null)
        {
            Match(some.ToFunc(), none?.ToFunc<int>() ?? (() => 42));
        }

        public TResult Match<TResult>(Func<T, TResult> some, Func<TResult> none)
        {
            var iAmSome = this as Some<T>;
            if (iAmSome != null)
            {
                return some(iAmSome.Value);
            }
            return none();
        }

        public TResult Match<TResult>(Func<T, TResult> some, TResult none)
        {
            var iAmSome = this as Some<T>;
            if (iAmSome != null)
            {
                return some(iAmSome.Value);
            }
            return none;
        }

        public async Task Match(Func<T, Task> some, Func<Task> none = null)
        {
            var iAmSome = this as Some<T>;
            if (iAmSome != null)
            {
                await some(iAmSome.Value).ConfigureAwait(false);
            }
            else if (none != null)
            {
                await none().ConfigureAwait(false);
            }
        }

        public async Task<TResult> Match<TResult>(Func<T, Task<TResult>> some, Func<Task<TResult>> none)
        {
            var iAmSome = this as Some<T>;
            if (iAmSome != null)
            {
                return await some(iAmSome.Value).ConfigureAwait(false);
            }
            return await none().ConfigureAwait(false);
        }

        public async Task<TResult> Match<TResult>(Func<T, Task<TResult>> some, TResult none)
        {
            var iAmSome = this as Some<T>;
            if (iAmSome != null)
            {
                return await some(iAmSome.Value).ConfigureAwait(false);
            }
            return none;
        }

        public static implicit operator Option<T>(T value) => Some(value);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public IEnumerator<T> GetEnumerator()
        {
            return Match(v => new[] { v }, Enumerable.Empty<T>).GetEnumerator();
        }

        public T GetValueOrDefault(Func<T> defaultValue = null)
        {
            return Match(v => v, () => defaultValue != null ? defaultValue() : default);
        }

        public T1 GetValueOrDefault<T1>(Func<T, T1> getValue, Func<T1> defaultValue = null)
        {
            return Match(getValue, () => defaultValue != null ? defaultValue() : default);
        }

        public bool IsNone() => !IsSome();

        public T GetValueOrThrow(string errorMessage = null)
        {
            return Match(v => v, () => { throw new InvalidOperationException(errorMessage ?? "Cannot access value of none option"); });
        }

        public Option<TBase> Convert<TBase>()
        {
            return Match(s => Some((TBase) (object)s), None<TBase>);
        }

        public Option<TTarget> As<TTarget>() where TTarget : class
        {
            return Convert(o => o.As<TTarget>());
        }

        public Option<TTarget> As<TTarget>(Func<T, object> propertyAccess) where TTarget : class
        {
            return Convert(o => propertyAccess(o).As<TTarget>());
        }

        public override string ToString()
        {
            return Match(v => v?.ToString(), () => GetType().BeautifulName());
        }
    }

    public class Some<T> : Option<T>
    {
        public T Value { get; }

        public Some(T value) => Value = value;

        bool Equals(Some<T> other) => EqualityComparer<T>.Default.Equals(Value, other.Value);

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((Some<T>)obj);
        }

        public override int GetHashCode() => EqualityComparer<T>.Default.GetHashCode(Value);

        public static bool operator ==(Some<T> left, Some<T> right) => Equals(left, right);

        public static bool operator !=(Some<T> left, Some<T> right) => !Equals(left, right);
    }

    public class None<T> : Option<T>
    {
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType();
        }

        public override int GetHashCode() => typeof(None<T>).GetHashCode();

        public static bool operator ==(None<T> left, None<T> right) => Equals(left, right);

        public static bool operator !=(None<T> left, None<T> right) => !Equals(left, right);
    }

    public static class FuncToAction
    {
        public static Action<T1> IgnoreReturn<T1, T2>(this Func<T1, T2> func)
        {
            return t => { func(t); };
        }

        public static Action IgnoreReturn<T>(this Func<T> func)
        {
            return () => { func(); };
        }

        public static Func<T> ToFunc<T>(this Action action)
        {
            return () =>
            {
                action();
                return default;
            };
        }

        public static Func<T, int> ToFunc<T>(this Action<T> action)
        {
            return t =>
            {
                action(t);
                return 42;
            };
        }

        public static Func<Task<T>> ToFunc<T>(this Func<Task> action)
        {
            return async () =>
            {
                await action().ConfigureAwait(false);
                return default;
            };
        }

        public static Func<T, Task<int>> ToFunc<T>(this Func<T, Task> action)
        {
            return async t =>
            {
                await action(t).ConfigureAwait(false);
                return 42;
            };
        }
    }

    public static class DictionaryExtension
    {
        public static Option<T> TryGetValue<TKey, T>(this IDictionary<TKey, T> dict, TKey key)
        {
            T value;
            if (!dict.TryGetValue(key, out value))
            {
                return Option<T>.None;
            }
            return value;
        }
    }

    public static class OptionExtension
    {
        public static Option<T> Flatten<T>(this Option<Option<T>> option)
        {
            return option.Match(s => s, () => Option<T>.None);
        }

        public static Option<T> ToOption<T>(this T item) where T : class => item ?? Option<T>.None;

        public static Option<TTarget> As<TTarget>(this object item) where TTarget : class => (item as TTarget).ToOption();

        public static string BeautifulName(this Type t)
        {
            if (!t.IsGenericType)
                if (!t.IsNested)
                    return t.Name;
                else
                {
                    return $"{t.DeclaringType.BeautifulName()}.{t.Name}";
                }

            try
            {
                var sb = new StringBuilder();

                var index = t.Name.LastIndexOf("`", StringComparison.Ordinal);
                if (index < 0)
                    return t.Name;


                sb.Append(t.Name.Substring(0, index));
                var i = 0;
                t.GetGenericArguments().Aggregate(sb, (a, type) => a.Append(i++ == 0 ? "<" : ",").Append(BeautifulName(type)));

                sb.Append(">");

                return sb.ToString();
            }
            catch (Exception)
            {
                return t.Name;
            }
        }
    }
}
