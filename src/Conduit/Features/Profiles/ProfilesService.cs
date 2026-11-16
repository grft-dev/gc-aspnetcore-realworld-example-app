using Conduit.Domain;
using System.Threading.Tasks;
using Conduit;
using Conduit.Infrastructure;
using Graftcode.Pipeline;

namespace Conduit.Features.Profiles;

public static class ProfilesService
{
    static ProfilesService()
    {
        ServicePipeline.Use(() => GraftHost.Services);
    }

    public static Task<Profile> Get(string? token, string username) =>
        ServicePipeline.RunAsync(
            nameof(ProfilesService),
            nameof(Get),
            ctx =>
                ctx.RunAuthorizedAsync(token, async () =>
                {
                    var handling = ctx.GetRequiredService<ProfilesHandling>();
                    return await handling.Get(new Details.Query(username), default);
                }),
            ("token", token),
            ("username", username)
        );
}
