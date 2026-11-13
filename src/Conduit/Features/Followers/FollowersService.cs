using System.Threading;
using System.Threading.Tasks;
using Conduit;
using Conduit.Features.Profiles;
using Conduit.Infrastructure;
using Graftcode.Pipeline;

namespace Conduit.Features.Followers;

public static class FollowersService
{
    static FollowersService()
    {
        ServicePipeline.Use(() => GraftHost.Services);
    }

    public static Task<ProfileEnvelope> Follow(string token, string username) =>
        ServicePipeline.RunAsync(
            nameof(FollowersService),
            nameof(Follow),
            async ctx =>
            {
                using (GraftUser.Begin(ctx, await GraftUser.Authenticate(ctx, token)))
                {
                    var handling = ctx.GetRequiredService<FollowersHandling>();
                    return await ctx.RunInTransactionAsync(
                        () => handling.Follow(username, CancellationToken.None),
                        CancellationToken.None
                    );
                }
            },
            ("token", token),
            ("username", username)
        );

    public static Task<ProfileEnvelope> Unfollow(string token, string username) =>
        ServicePipeline.RunAsync(
            nameof(FollowersService),
            nameof(Unfollow),
            async ctx =>
            {
                using (GraftUser.Begin(ctx, await GraftUser.Authenticate(ctx, token)))
                {
                    var handling = ctx.GetRequiredService<FollowersHandling>();
                    return await ctx.RunInTransactionAsync(
                        () => handling.Unfollow(username, CancellationToken.None),
                        CancellationToken.None
                    );
                }
            },
            ("token", token),
            ("username", username)
        );
}
