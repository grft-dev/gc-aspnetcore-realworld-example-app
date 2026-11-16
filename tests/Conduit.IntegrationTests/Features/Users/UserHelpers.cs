using System.Threading.Tasks;
using Conduit.Features.Users;

namespace Conduit.IntegrationTests.Features.Users;

public static class UserHelpers
{
    public static readonly string DefaultUserName = "username";

    /// <summary>
    /// creates a default user to be used in different tests
    /// </summary>
    /// <param name="fixture"></param>
    /// <returns></returns>
    public static async Task<User> CreateDefaultUser(SliceFixture fixture)
    {
        var command = new RegisterUserData(DefaultUserName, "email", "password");

        return await fixture.SendAsync<User>(command);
    }
}
