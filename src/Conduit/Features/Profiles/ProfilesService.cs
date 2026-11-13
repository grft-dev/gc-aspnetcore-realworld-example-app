using System.Threading;
using Conduit;
using System.Threading.Tasks;
using Graftcode.Pipeline;
using Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace Conduit.Features.Profiles;

public static class ProfilesService
{
    static ProfilesService()
    {
        ServicePipeline.Use(() => GraftHost.Services);
    }

    public static Task<ProfileEnvelope> Get(string? token, string username) =>
        ServicePipeline.RunAsync(
            nameof(ProfilesService),
            nameof(Get),
            async ctx =>
            {
                using (GraftUser.Begin(ctx, await GraftUser.Authenticate(ctx, token)))
                {
                    return await ctx.GetRequiredService<IMediator>()
                        .Send(new Details.Query(username), CancellationToken.None);
                }
            },
            ("token", token),
            ("username", username)
        );
}
