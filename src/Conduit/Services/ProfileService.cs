using Conduit.Graftcode;
using Conduit.Security;

namespace Conduit.Services;

public static class ProfileService
{
    public static string GetProfile(string token, string username) =>
        GraftAsync.Run(() => GetProfileAsync(token, username));

    public static string Follow(string token, string username) =>
        GraftAsync.Run(() => FollowAsync(token, username, follow: true));

    public static string Unfollow(string token, string username) =>
        GraftAsync.Run(() => FollowAsync(token, username, follow: false));

    private static async Task<string> GetProfileAsync(string token, string username)
    {
        var viewerId = TokenIssuer.ReadOptional(token);
        var repository = ConduitGate.Repository();
        var person =
            await repository.GetPersonByUsernameAsync(username).ConfigureAwait(false)
            ?? throw new InvalidOperationException("profile not found");
        var following =
            viewerId != null
            && await repository
                .IsFollowingAsync(viewerId.Value, person.PersonId)
                .ConfigureAwait(false);
        return RecordFormat.Profile(person, following);
    }

    private static async Task<string> FollowAsync(string token, string username, bool follow)
    {
        var personId = TokenIssuer.RequirePersonId(token);
        var repository = ConduitGate.Repository();
        var observer =
            await repository.GetPersonByIdAsync(personId).ConfigureAwait(false)
            ?? throw new InvalidOperationException("user not found");
        var target =
            await repository.GetPersonByUsernameAsync(username).ConfigureAwait(false)
            ?? throw new InvalidOperationException("profile not found");
        if (follow)
        {
            await repository.FollowAsync(observer.PersonId, target.PersonId).ConfigureAwait(false);
        }
        else
        {
            await repository
                .UnfollowAsync(observer.PersonId, target.PersonId)
                .ConfigureAwait(false);
        }

        return RecordFormat.Profile(target, follow);
    }
}
