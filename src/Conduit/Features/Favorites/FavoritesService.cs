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
            ctx =>
                ctx.RunAuthorizedTransactionAsync(token, async () =>
                {
                    var handling = ctx.GetRequiredService<FavoritesHandling>();
                    var article = await (
                        add
                            ? handling.Add(slug, default)
                            : handling.Remove(slug, default)
                    );
                    return ArticleViews.From(article);
                }),
            ("token", token),
            ("slug", slug)
        );
}
