using System.Threading;
using System.Threading.Tasks;
using Conduit.Domain;

namespace Conduit.Features.Profiles;

public interface IProfileReader
{
    public Task<Profile> ReadProfile(string username, CancellationToken cancellationToken);
}
