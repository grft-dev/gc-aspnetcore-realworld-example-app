using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Domain;
using Conduit.Features.Profiles;
using Conduit.Infrastructure;
using Conduit.Infrastructure.Errors;
using Microsoft.EntityFrameworkCore;

namespace Conduit.Features.Followers;

public sealed class FollowersHandling(
    ConduitContext context,
    ICurrentUserAccessor currentUserAccessor,
    IProfileReader profileReader
)
{
    public async Task<ProfileEnvelope> Follow(string username, CancellationToken cancellationToken)
    {
        var target = await context.Persons.FirstOrDefaultAsync(
            x => x.Username == username,
            cancellationToken
        );

        if (target is null)
        {
            throw new RestException(HttpStatusCode.NotFound, "profile", Constants.NOT_FOUND);
        }

        var observer = await context.Persons.FirstOrDefaultAsync(
            x => x.PersonId == currentUserAccessor.GetCurrentPersonId(),
            cancellationToken
        );

        if (observer is null)
        {
            throw new RestException(HttpStatusCode.NotFound, "user", Constants.NOT_FOUND);
        }

        var followedPeople = await context.FollowedPeople.FirstOrDefaultAsync(
            x => x.ObserverId == observer.PersonId && x.TargetId == target.PersonId,
            cancellationToken
        );

        if (followedPeople == null)
        {
            followedPeople = new FollowedPeople
            {
                Observer = observer,
                ObserverId = observer.PersonId,
                Target = target,
                TargetId = target.PersonId,
            };
            await context.FollowedPeople.AddAsync(followedPeople, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        return await profileReader.ReadProfile(username, cancellationToken);
    }

    public async Task<ProfileEnvelope> Unfollow(
        string username,
        CancellationToken cancellationToken
    )
    {
        var target = await context.Persons.FirstOrDefaultAsync(
            x => x.Username == username,
            cancellationToken
        );

        if (target is null)
        {
            throw new RestException(HttpStatusCode.NotFound, "profile", Constants.NOT_FOUND);
        }

        var observer = await context.Persons.FirstOrDefaultAsync(
            x => x.PersonId == currentUserAccessor.GetCurrentPersonId(),
            cancellationToken
        );

        if (observer is null)
        {
            throw new RestException(HttpStatusCode.NotFound, "user", Constants.NOT_FOUND);
        }

        var followedPeople = await context.FollowedPeople.FirstOrDefaultAsync(
            x => x.ObserverId == observer.PersonId && x.TargetId == target.PersonId,
            cancellationToken
        );

        if (followedPeople != null)
        {
            context.FollowedPeople.Remove(followedPeople);
            await context.SaveChangesAsync(cancellationToken);
        }

        return await profileReader.ReadProfile(username, cancellationToken);
    }
}
