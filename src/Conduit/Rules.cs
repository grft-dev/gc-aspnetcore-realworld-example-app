namespace Conduit;

internal static class Rules
{
    public static string Required(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{field} can't be blank");
        }

        return value.Trim();
    }

    public static void MaxLength(string value, int max, string field)
    {
        if (value.Length > max)
        {
            throw new InvalidOperationException($"{field} is too long");
        }
    }

    public static void Password(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("password can't be blank");
        }

        if (password.Length < 8)
        {
            throw new InvalidOperationException("password is too short (minimum is 8 characters)");
        }
    }

    public static string? KeepOrReplace(string? value, string field, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            throw new InvalidOperationException($"{field} can't be blank");
        }

        MaxLength(trimmed, maxLength, field);
        return trimmed;
    }

    public static string? KeepOrRequired(string? value, string field) =>
        string.IsNullOrEmpty(value) ? null : Required(value, field);

    public static string? KeepOrText(string? value) => string.IsNullOrEmpty(value) ? null : value;

    public static string[] Tags(string? tags)
    {
        if (string.IsNullOrWhiteSpace(tags))
        {
            return [];
        }

        string[] parsed =
        [
            .. tags.Split(
                    ',',
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
                )
                .Distinct(StringComparer.Ordinal),
        ];
        foreach (var tag in parsed)
        {
            MaxLength(tag, 256, "tag");
        }

        return parsed;
    }

    public static (int Limit, int Offset) Page(int limit, int offset)
    {
        var take = limit <= 0 ? 20 : limit;
        var skip = offset < 0 ? 0 : offset;
        return (take, skip);
    }
}
