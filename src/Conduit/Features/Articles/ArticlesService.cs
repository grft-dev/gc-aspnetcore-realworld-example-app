using System.Threading;
using System.Threading.Tasks;
using Conduit;
using Graftcode.Pipeline;
using Mediator;
using Microsoft.Extensions.DependencyInjection;

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
                    return await ctx.GetRequiredService<IMediator>()
                        .Send(
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
                    return await ctx.GetRequiredService<IMediator>()
                        .Send(
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
                    var envelope = await ctx.GetRequiredService<IMediator>()
                        .Send(new Details.Query(slug), CancellationToken.None);
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
                    var envelope = await ctx.GetRequiredService<IMediator>()
                        .Send(
                            new Create.Command(
                                new Create.ArticleData
                                {
                                    Title = title,
                                    Description = description,
                                    Body = body,
                                    TagList = SplitTags(tags),
                                }
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
                    var article = new Edit.ArticleData
                    {
                        Title = title,
                        Description = description,
                        Body = body,
                    };
                    if (tags is not null)
                    {
                        article.TagList = SplitTags(tags);
                    }

                    var envelope = await ctx.GetRequiredService<IMediator>()
                        .Send(new Edit.Command(new Edit.Model(article), slug), CancellationToken.None);
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
                    await ctx.GetRequiredService<IMediator>()
                        .Send(new Delete.Command(slug), CancellationToken.None);
                }
            },
            ("token", token),
            ("slug", slug)
        );

    private static string[]? SplitTags(string? tags) =>
        string.IsNullOrEmpty(tags)
            ? null
            : tags.Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);
}
