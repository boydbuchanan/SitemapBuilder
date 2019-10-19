namespace Sitemap
{
    internal static class Extensions
    {
        public static string TrimWith(this string str, char trimWith)
        {
            return str.TrimEnd(trimWith) + trimWith;
        }
    }
}
