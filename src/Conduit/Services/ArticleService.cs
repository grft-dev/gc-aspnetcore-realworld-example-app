using Conduit.Graftcode;
using Conduit.Security;

namespace Conduit.Services;

public static class ArticleService
{
    public static string[] ListArticles(
        string token,
        string tag,
        string author,
        string favorited,
        int limit,
        int offset
    ) => GraftAsync.Run(() => ListAsync(token, tag, author, favorited, limit, offset, feed: false));

    public static string[] Feed(
        string token,
        string tag,
        string author,
        string favorited,
        int limit,
        int offset
    ) => GraftAsync.Run(() => ListAsync(token, tag, author, favorited, limit, offset, feed: true));

    public static string GetArticle(string token, string slug) =>
        GraftAsync.Run(() => GetAsync(token, slug));

    public static string CreateArticle(
        string token,
        string title,
        string description,
        string body,
        string tags
    ) => GraftAsync.Run(() => CreateAsync(token, title, description, body, tags));

    public static string EditArticle(
        string token,
        string slug,
        string title,
        string description,
        string body,
        string tags
    ) => GraftAsync.Run(() => EditAsync(token, slug, title, description, body, tags));

    public static string DeleteArticle(string token, string slug) =>
        GraftAsync.Run(() => DeleteAsync(token, slug));

    public static string FavoriteArticle(string token, string slug) =>
        GraftAsync.Run(() => FavoriteAsync(token, slug, favorite: true));

    public static string UnfavoriteArticle(string token, string slug) =>
        GraftAsync.Run(() => FavoriteAsync(token, slug, favorite: false));

    private static async Task<string[]> ListAsync(
        string token,
        string tag,
        string author,
        string favorited,
        int limit,
        int offset,
        bool feed
    )
    {
        var viewerId = feed ? TokenIssuer.RequirePersonId(token) : TokenIssuer.ReadOptional(token);
        var (take, skip) = Rules.Page(limit, offset);
        var articles = await ConduitGate
            .Repository()
            .ListArticlesAsync(
                viewerId,
                tag ?? string.Empty,
                author ?? string.Empty,
                favorited ?? string.Empty,
                take,
                skip,
                feed
            )
            .ConfigureAwait(false);
        return [.. articles.Select(article => RecordFormat.Article(article, includeBody: false))];
    }

    private static async Task<string> GetAsync(string token, string slug)
    {
        var viewerId = TokenIssuer.ReadOptional(token);
        var article =
            await ConduitGate
                .Repository()
                .GetArticleBySlugAsync(slug, viewerId)
                .ConfigureAwait(false)
            ?? throw new InvalidOperationException("article not found");
        return RecordFormat.Article(article, includeBody: true);
    }

    private static async Task<string> CreateAsync(
        string token,
        string title,
        string description,
        string body,
        string tags
    )
    {
        var personId = TokenIssuer.RequirePersonId(token);
        var repository = ConduitGate.Repository();
        _ =
            await repository.GetPersonByIdAsync(personId).ConfigureAwait(false)
            ?? throw new InvalidOperationException("user not found");
        var article = await repository
            .CreateArticleAsync(
                personId,
                Rules.Required(title, "title"),
                Rules.Required(description, "description"),
                Rules.Required(body, "body"),
                Rules.Tags(tags)
            )
            .ConfigureAwait(false);
        return RecordFormat.Article(article, includeBody: true);
    }

    private static async Task<string> EditAsync(
        string token,
        string slug,
        string title,
        string description,
        string body,
        string tags
    )
    {
        var personId = TokenIssuer.RequirePersonId(token);
        var repository = ConduitGate.Repository();
        var article =
            await repository.GetArticleBySlugAsync(slug, personId).ConfigureAwait(false)
            ?? throw new InvalidOperationException("article not found");
        if (article.AuthorId != personId)
        {
            throw new InvalidOperationException("article forbidden");
        }

        var nextTitle = Rules.KeepOrRequired(title, "title") ?? article.Title;
        var nextDescription =
            Rules.KeepOrRequired(description, "description") ?? article.Description;
        var nextBody = Rules.KeepOrRequired(body, "body") ?? article.Body;
        var nextTags = Rules.Tags(tags);
        var titleChanged = !string.Equals(nextTitle, article.Title, StringComparison.Ordinal);
        var tagsChanged = !article.Tags.ToHashSet(StringComparer.Ordinal).SetEquals(nextTags);
        var textChanged =
            titleChanged
            || !string.Equals(nextDescription, article.Description, StringComparison.Ordinal)
            || !string.Equals(nextBody, article.Body, StringComparison.Ordinal);
        if (!textChanged && !tagsChanged)
        {
            return RecordFormat.Article(article, includeBody: true);
        }

        var updated = await repository
            .UpdateArticleAsync(
                article,
                nextTitle,
                nextDescription,
                nextBody,
                nextTags,
                titleChanged
            )
            .ConfigureAwait(false);
        return RecordFormat.Article(updated, includeBody: true);
    }

    private static async Task<string> DeleteAsync(string token, string slug)
    {
        var personId = TokenIssuer.RequirePersonId(token);
        var repository = ConduitGate.Repository();
        var article =
            await repository.GetArticleBySlugAsync(slug, personId).ConfigureAwait(false)
            ?? throw new InvalidOperationException("article not found");
        if (article.AuthorId != personId)
        {
            throw new InvalidOperationException("article forbidden");
        }

        await repository.DeleteArticleAsync(article.ArticleId).ConfigureAwait(false);
        return string.Empty;
    }

    private static async Task<string> FavoriteAsync(string token, string slug, bool favorite)
    {
        var personId = TokenIssuer.RequirePersonId(token);
        var repository = ConduitGate.Repository();
        _ =
            await repository.GetPersonByIdAsync(personId).ConfigureAwait(false)
            ?? throw new InvalidOperationException("user not found");
        var article =
            await repository.GetArticleBySlugAsync(slug, personId).ConfigureAwait(false)
            ?? throw new InvalidOperationException("article not found");
        if (favorite)
        {
            await repository.FavoriteAsync(article.ArticleId, personId).ConfigureAwait(false);
        }
        else
        {
            await repository.UnfavoriteAsync(article.ArticleId, personId).ConfigureAwait(false);
        }

        var updated =
            await repository.GetArticleBySlugAsync(slug, personId).ConfigureAwait(false)
            ?? throw new InvalidOperationException("article not found");
        return RecordFormat.Article(updated, includeBody: true);
    }
}
