using System.Linq;
using System.Threading;
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
            async ctx =>
            {
                using (GraftUser.Begin(ctx, await GraftUser.Authenticate(ctx, token)))
                {
                    var handling = ctx.GetRequiredService<CommentsHandling>();
                    var envelope = await ctx.RunInTransactionAsync(
                        () =>
                            handling.Create(
                                new Create.CommentData(body),
                                slug,
                                CancellationToken.None
                            ),
                        CancellationToken.None
                    );
                    return From(envelope.Comment);
                }
            },
            ("token", token),
            ("slug", slug),
            ("body", body)
        );

    public static Task<CommentView[]> List(string? token, string slug) =>
        ServicePipeline.RunAsync(
            nameof(CommentsService),
            nameof(List),
            async ctx =>
            {
                using (GraftUser.Begin(ctx, await GraftUser.Authenticate(ctx, token)))
                {
                    var handling = ctx.GetRequiredService<CommentsHandling>();
                    var envelope = await handling.List(
                        new List.Query(slug),
                        CancellationToken.None
                    );
                    return envelope.Comments.Select(From).ToArray();
                }
            },
            ("token", token),
            ("slug", slug)
        );

    public static Task Delete(string token, string slug, int id) =>
        ServicePipeline.RunAsync(
            nameof(CommentsService),
            nameof(Delete),
            async ctx =>
            {
                using (GraftUser.Begin(ctx, await GraftUser.Authenticate(ctx, token)))
                {
                    var handling = ctx.GetRequiredService<CommentsHandling>();
                    await ctx.RunInTransactionAsync(
                        () => handling.Delete(slug, id, CancellationToken.None),
                        CancellationToken.None
                    );
                }
            },
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
