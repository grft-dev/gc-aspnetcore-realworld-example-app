using System.Threading;
using Conduit;
using System.Threading.Tasks;
using Conduit.Features.Profiles;
using Graftcode.Pipeline;
using Mediator;
using Microsoft.Extensions.DependencyInjection;

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
                    return await ctx.GetRequiredService<IMediator>()
                        .Send(new Add.Command(username), CancellationToken.None);
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
                    return await ctx.GetRequiredService<IMediator>()
                        .Send(new Delete.Command(username), CancellationToken.None);
                }
            },
            ("token", token),
            ("username", username)
        );
}
