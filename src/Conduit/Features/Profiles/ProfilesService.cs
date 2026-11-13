using System.Threading;
using System.Threading.Tasks;
using Conduit;
using Graftcode.Pipeline;

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
                    var handling = ctx.GetRequiredService<ProfilesHandling>();
                    return await handling.Get(new Details.Query(username), CancellationToken.None);
                }
            },
            ("token", token),
            ("username", username)
        );
}
