using System.Collections.Generic;

namespace Switchyard.CodeGeneration
{
    public static class EnumerableExtension
    {
        public static string ToSeparatedString<T>(this IEnumerable<T> values, string separator = ",") => string.Join(separator, values);
    }
}