using System.Threading;
using System.Threading.Tasks;
using Conduit;
using Conduit.Features.Articles;
using Graftcode.Pipeline;
using Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace Conduit.Features.Favorites;

public static class FavoritesService
{
    static FavoritesService()
    {
        ServicePipeline.Use(() => GraftHost.Services);
    }

    public static Task<ArticleSummary> Add(string token, string slug) =>
        Change(token, slug, true);

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
                    var mediator = ctx.GetRequiredService<IMediator>();
                    var envelope = add
                        ? await mediator.Send(new Add.Command(slug), CancellationToken.None)
                        : await mediator.Send(new Delete.Command(slug), CancellationToken.None);
                    return ArticleViews.From(envelope.Article);
                }
            },
            ("token", token),
            ("slug", slug)
        );
}
