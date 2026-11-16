using System.Threading;
using System.Threading.Tasks;
using Conduit;
using Conduit.Infrastructure;
using Graftcode.Pipeline;

namespace Conduit.Features.Articles;

public static class ArticlesService
{
    static ArticlesService()
    {
        ServicePipeline.Use(() => GraftHost.Services);
    }

    public static Task<ArticlesEnvelope> List(
        string? token,
        string? tag,
        string? author,
        string? favorited,
        int? limit,
        int? offset
    ) =>
        ServicePipeline.RunAsync(
            nameof(ArticlesService),
            nameof(List),
            async ctx =>
            {
                using (GraftUser.Begin(ctx, await GraftUser.Authenticate(ctx, token)))
                {
                    var handling = ctx.GetRequiredService<ArticlesHandling>();
                    return await handling.List(
                        new List.Query(tag, author, favorited, limit, offset),
                        CancellationToken.None
                    );
                }
            },
            ("token", token),
            ("tag", tag),
            ("author", author),
            ("favorited", favorited),
            ("limit", limit),
            ("offset", offset)
        );

    public static Task<ArticlesEnvelope> Feed(
        string token,
        string? tag,
        string? author,
        string? favorited,
        int? limit,
        int? offset
    ) =>
        ServicePipeline.RunAsync(
            nameof(ArticlesService),
            nameof(Feed),
            async ctx =>
            {
                using (GraftUser.Begin(ctx, await GraftUser.Authenticate(ctx, token)))
                {
                    var handling = ctx.GetRequiredService<ArticlesHandling>();
                    return await handling.List(
                        new List.Query(tag, author, favorited, limit, offset) { IsFeed = true },
                        CancellationToken.None
                    );
                }
            },
            ("token", token),
            ("tag", tag),
            ("author", author),
            ("favorited", favorited),
            ("limit", limit),
            ("offset", offset)
        );

    public static Task<ArticleSummary> Get(string? token, string slug) =>
        ServicePipeline.RunAsync(
            nameof(ArticlesService),
            nameof(Get),
            async ctx =>
            {
                using (GraftUser.Begin(ctx, await GraftUser.Authenticate(ctx, token)))
                {
                    var handling = ctx.GetRequiredService<ArticlesHandling>();
                    var envelope = await handling.Details(
                        new Details.Query(slug),
                        CancellationToken.None
                    );
                    return ArticleViews.From(envelope.Article);
                }
            },
            ("token", token),
            ("slug", slug)
        );

    public static Task<ArticleSummary> Create(
        string token,
        string title,
        string description,
        string body,
        string? tags
    ) =>
        ServicePipeline.RunAsync(
            nameof(ArticlesService),
            nameof(Create),
            async ctx =>
            {
                using (GraftUser.Begin(ctx, await GraftUser.Authenticate(ctx, token)))
                {
                    var handling = ctx.GetRequiredService<ArticlesHandling>();
                    var envelope = await ctx.RunInTransactionAsync(
                        () =>
                            handling.Create(
                                new CreateArticleData
                                {
                                    Title = title,
                                    Description = description,
                                    Body = body,
                                    TagList = SplitTags(tags),
                                },
                                CancellationToken.None
                            ),
                        CancellationToken.None
                    );
                    return ArticleViews.From(envelope.Article);
                }
            },
            ("token", token),
            ("title", title),
            ("description", description),
            ("body", body),
            ("tags", tags)
        );

    public static Task<ArticleSummary> Edit(
        string token,
        string slug,
        string? title,
        string? description,
        string? body,
        string? tags
    ) =>
        ServicePipeline.RunAsync(
            nameof(ArticlesService),
            nameof(Edit),
            async ctx =>
            {
                using (GraftUser.Begin(ctx, await GraftUser.Authenticate(ctx, token)))
                {
                    var article = new EditArticleData
                    {
                        Title = title,
                        Description = description,
                        Body = body,
                    };
                    if (tags is not null)
                    {
                        article.TagList = SplitTags(tags);
                    }

                    var handling = ctx.GetRequiredService<ArticlesHandling>();
                    var envelope = await ctx.RunInTransactionAsync(
                        () => handling.Edit(article, slug, CancellationToken.None),
                        CancellationToken.None
                    );
                    return ArticleViews.From(envelope.Article);
                }
            },
            ("token", token),
            ("slug", slug),
            ("title", title),
            ("description", description),
            ("body", body),
            ("tags", tags)
        );

    public static Task Delete(string token, string slug) =>
        ServicePipeline.RunAsync(
            nameof(ArticlesService),
            nameof(Delete),
            async ctx =>
            {
                using (GraftUser.Begin(ctx, await GraftUser.Authenticate(ctx, token)))
                {
                    var handling = ctx.GetRequiredService<ArticlesHandling>();
                    await ctx.RunInTransactionAsync(
                        () => handling.Delete(slug, CancellationToken.None),
                        CancellationToken.None
                    );
                }
            },
            ("token", token),
            ("slug", slug)
        );

    private static string[]? SplitTags(string? tags) =>
        string.IsNullOrEmpty(tags)
            ? null
            : tags.Split(
                ',',
                System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries
            );
}
