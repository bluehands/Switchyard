namespace Switchyard.CodeGeneration
{
    public static class StringExtension
    {
        public static string FirstToLower(this string name) => string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name.Substring(1);
        public static string FirstToUpper(this string name) => string.IsNullOrEmpty(name) ? name : char.ToUpperInvariant(name[0]) + name.Substring(1);
    }
}