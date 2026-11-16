using Conduit.Domain;
using System.Threading;
using System.Threading.Tasks;

namespace Conduit.Features.Profiles;

public interface IProfileReader
{
    public Task<Profile> ReadProfile(string username, CancellationToken cancellationToken);
}
