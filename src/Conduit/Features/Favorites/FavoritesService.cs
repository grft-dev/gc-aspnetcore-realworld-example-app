using System.Threading;
using System.Threading.Tasks;
using Conduit;
using Conduit.Features.Articles;
using Conduit.Infrastructure;
using Graftcode.Pipeline;

namespace Conduit.Features.Favorites;

public static class FavoritesService
{
    static FavoritesService()
    {
        ServicePipeline.Use(() => GraftHost.Services);
    }

    public static Task<ArticleSummary> Add(string token, string slug) => Change(token, slug, true);

    public static Task<ArticleSummary> Remove(string token, string slug) =>
        Change(token, slug, false);

    private static Task<ArticleSummary> Change(string token, string slug, bool add) =>
        ServicePipeline.RunAsync(
            nameof(FavoritesService),
            add ? nameof(Add) : nameof(Remove),
            async ctx =>
            {
                using (GraftUser.Begin(ctx, await GraftUser.Authenticate(ctx, token)))
                {
                    var handling = ctx.GetRequiredService<FavoritesHandling>();
                    var envelope = await ctx.RunInTransactionAsync(
                        () =>
                            add
                                ? handling.Add(slug, CancellationToken.None)
                                : handling.Remove(slug, CancellationToken.None),
                        CancellationToken.None
                    );
                    return ArticleViews.From(envelope.Article);
                }
            },
            ("token", token),
            ("slug", slug)
        );
}
