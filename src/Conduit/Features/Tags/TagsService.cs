using System.Collections.Generic;
using System.Threading.Tasks;
using Conduit;
using Conduit.Infrastructure;
using Graftcode.Pipeline;

namespace Conduit.Features.Tags;

public static class TagsService
{
    static TagsService()
    {
        ServicePipeline.Use(() => GraftHost.Services);
    }

    public static Task<List<string>> List() =>
        ServicePipeline.RunAsync(
            nameof(TagsService),
            nameof(List),
            ctx =>
                ctx.RunAuthorizedAsync(null, async () =>
                {
                    var handling = ctx.GetRequiredService<TagsHandling>();
                    return await handling.List(new List.Query(), default);
                })
        );
}
