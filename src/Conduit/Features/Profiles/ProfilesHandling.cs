using System.Threading;
using System.Threading.Tasks;
using Conduit.Domain;

namespace Conduit.Features.Profiles;

public sealed class ProfilesHandling(IProfileReader profileReader)
{
    public Task<Profile> Get(Details.Query message, CancellationToken cancellationToken) =>
        profileReader.ReadProfile(message.Username, cancellationToken);
}
