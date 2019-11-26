namespace Switchyard.CodeGeneration
{
    public static class StringExtension
    {
        public static string FirstToLower(this string name) => char.ToLowerInvariant(name[0]) + name.Substring(1);
    }
}