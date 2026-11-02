using System.Text.RegularExpressions;

namespace Conduit;

internal static partial class Slug
{
    public static string Generate(string phrase)
    {
        var str = phrase.ToLowerInvariant();
        str = InvalidCharsRegex().Replace(str, "");
        str = MultipleSpacesRegex().Replace(str, " ").Trim();
        var length = str.Length <= 45 ? str.Length : 45;
        str = str[..length].Trim();
        return SpaceRegex().Replace(str, "-");
    }

    [GeneratedRegex("[^a-z0-9\\s-]")]
    private static partial Regex InvalidCharsRegex();

    [GeneratedRegex("\\s+")]
    private static partial Regex MultipleSpacesRegex();

    [GeneratedRegex("\\s")]
    private static partial Regex SpaceRegex();
}
