using System.Threading.Tasks;
using Conduit;
using Conduit.Domain;
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

    public static Task<Profile> Follow(string token, string username) =>
        ServicePipeline.RunAsync(
            nameof(FollowersService),
            nameof(Follow),
            ctx =>
                ctx.RunAuthorizedTransactionAsync(
                    token,
                    async () =>
                    {
                        var handling = ctx.GetRequiredService<FollowersHandling>();
                        return await handling.Follow(username, default);
                    }
                ),
            ("token", token),
            ("username", username)
        );

    public static Task<Profile> Unfollow(string token, string username) =>
        ServicePipeline.RunAsync(
            nameof(FollowersService),
            nameof(Unfollow),
            ctx =>
                ctx.RunAuthorizedTransactionAsync(
                    token,
                    async () =>
                    {
                        var handling = ctx.GetRequiredService<FollowersHandling>();
                        return await handling.Unfollow(username, default);
                    }
                ),
            ("token", token),
            ("username", username)
        );
}
