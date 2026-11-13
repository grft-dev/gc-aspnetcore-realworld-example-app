using System.Threading;
using Conduit;
using System.Threading.Tasks;
using Graftcode.Pipeline;
using Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace Conduit.Features.Tags;

public static class TagsService
{
    static TagsService()
    {
        ServicePipeline.Use(() => GraftHost.Services);
    }

    public static Task<TagsEnvelope> List() =>
        ServicePipeline.RunAsync(
            nameof(TagsService),
            nameof(List),
            async ctx =>
            {
                using (GraftUser.Begin(ctx, await GraftUser.Authenticate(ctx, null)))
                {
                    return await ctx.GetRequiredService<IMediator>()
                        .Send(new List.Query(), CancellationToken.None);
                }
            }
        );
}
