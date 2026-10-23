using Conduit.Graftcode;
using Conduit.Security;

namespace Conduit.Services;

public static class CommentService
{
    public static string CreateComment(string token, string slug, string body) =>
        GraftAsync.Run(() => CreateAsync(token, slug, body));

    public static string[] ListComments(string token, string slug) =>
        GraftAsync.Run(() => ListAsync(token, slug));

    public static string DeleteComment(string token, string slug, int id) =>
        GraftAsync.Run(() => DeleteAsync(token, slug, id));

    private static async Task<string> CreateAsync(string token, string slug, string body)
    {
        var personId = TokenIssuer.RequirePersonId(token);
        var text = Rules.Required(body, "body");
        var repository = ConduitGate.Repository();
        _ =
            await repository.GetPersonByIdAsync(personId).ConfigureAwait(false)
            ?? throw new InvalidOperationException("user not found");
        var article =
            await repository.GetArticleBySlugAsync(slug, personId).ConfigureAwait(false)
            ?? throw new InvalidOperationException("article not found");
        var comment = await repository
            .CreateCommentAsync(article.ArticleId, personId, text)
            .ConfigureAwait(false);
        return RecordFormat.Comment(comment);
    }

    private static async Task<string[]> ListAsync(string token, string slug)
    {
        var viewerId = TokenIssuer.ReadOptional(token);
        var repository = ConduitGate.Repository();
        var article =
            await repository.GetArticleBySlugAsync(slug, viewerId).ConfigureAwait(false)
            ?? throw new InvalidOperationException("article not found");
        var comments = await repository
            .ListCommentsAsync(article.ArticleId, viewerId)
            .ConfigureAwait(false);
        return [.. comments.Select(RecordFormat.Comment)];
    }

    private static async Task<string> DeleteAsync(string token, string slug, int id)
    {
        var personId = TokenIssuer.RequirePersonId(token);
        var repository = ConduitGate.Repository();
        var article =
            await repository.GetArticleBySlugAsync(slug, personId).ConfigureAwait(false)
            ?? throw new InvalidOperationException("article not found");
        var comment =
            await repository.GetCommentAsync(id, personId).ConfigureAwait(false)
            ?? throw new InvalidOperationException("comment not found");
        if (comment.ArticleId != article.ArticleId)
        {
            throw new InvalidOperationException("comment not found");
        }

        if (comment.AuthorId != personId)
        {
            throw new InvalidOperationException("comment forbidden");
        }

        await repository.DeleteCommentAsync(comment.CommentId).ConfigureAwait(false);
        return string.Empty;
    }
}
