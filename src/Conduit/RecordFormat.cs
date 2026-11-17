using System.Globalization;
using Conduit.Repositories;

namespace Conduit;

internal static class RecordFormat
{
    public static string User(PersonRow person, string token) =>
        Join(person.Email, token, person.Username, person.Bio, person.Image);

    public static string Profile(PersonRow person, bool following) =>
        Join(person.Username, person.Bio, person.Image, Flag(following));

    public static string Article(ArticleRow article, bool includeBody) =>
        Join(
            article.Slug,
            article.Title,
            article.Description,
            includeBody ? article.Body : string.Empty,
            Timestamp(article.CreatedAt),
            Timestamp(article.UpdatedAt),
            Flag(article.Favorited),
            article.FavoritesCount.ToString(CultureInfo.InvariantCulture),
            Tags(article.Tags),
            article.AuthorUsername,
            article.AuthorBio,
            article.AuthorImage,
            Flag(article.AuthorFollowing)
        );

    public static string Comment(CommentRow comment) =>
        Join(
            comment.CommentId.ToString(CultureInfo.InvariantCulture),
            Timestamp(comment.CreatedAt),
            Timestamp(comment.UpdatedAt),
            comment.Body,
            comment.AuthorUsername,
            comment.AuthorBio,
            comment.AuthorImage,
            Flag(comment.AuthorFollowing)
        );

    public static string Timestamp(DateTime value)
    {
        var utc =
            value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return utc.ToString("o", CultureInfo.InvariantCulture);
    }

    private static string Tags(IEnumerable<string> tags) =>
        string.Join(',', tags.Select(EscapeTag));

    private static string EscapeTag(string tag) =>
        tag.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(",", "\\,", StringComparison.Ordinal);

    private static string Flag(bool value) => value ? "true" : "false";

    private static string Join(params string?[] fields) => string.Join('|', fields.Select(Escape));

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("|", "\\|", StringComparison.Ordinal);
    }
}
