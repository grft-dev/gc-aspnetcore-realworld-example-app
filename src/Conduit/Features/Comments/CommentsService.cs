using System.Linq;
using System.Threading.Tasks;
using Conduit;
using Conduit.Infrastructure;
using Graftcode.Pipeline;

namespace Conduit.Features.Comments;

public sealed class CommentView
{
    public int Id { get; init; }

    public string? Body { get; init; }

    public string? Author { get; init; }
}

public static class CommentsService
{
    static CommentsService()
    {
        ServicePipeline.Use(() => GraftHost.Services);
    }

    public static Task<CommentView> Create(string token, string slug, string body) =>
        ServicePipeline.RunAsync(
            nameof(CommentsService),
            nameof(Create),
            ctx =>
                ctx.RunAuthorizedTransactionAsync(token, async () =>
                {
                    var handling = ctx.GetRequiredService<CommentsHandling>();
                    var comment = await handling.Create(new CreateCommentData(body), slug, default);
                    return From(comment);
                }),
            ("token", token),
            ("slug", slug),
            ("body", body)
        );

    public static Task<CommentView[]> List(string? token, string slug) =>
        ServicePipeline.RunAsync(
            nameof(CommentsService),
            nameof(List),
            ctx =>
                ctx.RunAuthorizedAsync(token, async () =>
                {
                    var handling = ctx.GetRequiredService<CommentsHandling>();
                    var comments = await handling.List(new List.Query(slug), default);
                    return comments.Select(From).ToArray();
                }),
            ("token", token),
            ("slug", slug)
        );

    public static Task Delete(string token, string slug, int id) =>
        ServicePipeline.RunAsync(
            nameof(CommentsService),
            nameof(Delete),
            ctx =>
                ctx.RunAuthorizedTransactionAsync(token, async () =>
                {
                    var handling = ctx.GetRequiredService<CommentsHandling>();
                    await handling.Delete(slug, id, default);
                }),
            ("token", token),
            ("slug", slug),
            ("id", id)
        );

    private static CommentView From(Domain.Comment comment) =>
        new()
        {
            Id = comment.CommentId,
            Body = comment.Body,
            Author = comment.Author?.Username,
        };
}
