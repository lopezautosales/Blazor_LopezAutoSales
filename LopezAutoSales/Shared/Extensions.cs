using System.Globalization;

namespace LopezAutoSales.Shared
{
    public static class Extensions
    {
        public static string ToTitleCase(this string s) =>
    CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLower());
    }
}
