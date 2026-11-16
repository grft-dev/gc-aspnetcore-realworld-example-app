using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Conduit.Features;
using Conduit.Features.Users;
using Conduit.Infrastructure.Errors;
using Conduit.Infrastructure.Security;
using Xunit;

namespace Conduit.IntegrationTests.Features.Users;

public class EditTests : SliceFixture
{
    private UserHandling CreateHandling(string currentUser) =>
        new(
            GetDbContext(),
            new PasswordHasher(),
            new StubCurrentUserAccessor(
                currentUser,
                GetDbContext().Persons.Single(x => x.Username == currentUser).PersonId
            ),
            new StubJwtTokenGenerator(),
            new ConduitMapper()
        );

    [Fact]
    public async Task Expect_Edit_User_Username_To_Duplicate_Throws_Conflict()
    {
        await SendAsync(new RegisterUserData("user1", "user1@example.com", "password1"));
        await SendAsync(new RegisterUserData("user2", "user2@example.com", "password2"));

        var userData = new EditUserData();
        userData.Username = "user1";

        var ex = await Assert.ThrowsAsync<RestException>(() =>
            CreateHandling("user2").Update(userData, default)
        );

        Assert.Equal(HttpStatusCode.Conflict, ex.Code);
    }

    [Fact]
    public async Task Expect_Edit_User_Email_To_Duplicate_Throws_Conflict()
    {
        await SendAsync(new RegisterUserData("user3", "user3@example.com", "password3"));
        await SendAsync(new RegisterUserData("user4", "user4@example.com", "password4"));

        var userData = new EditUserData();
        userData.Email = "user3@example.com";

        var ex = await Assert.ThrowsAsync<RestException>(() =>
            CreateHandling("user4").Update(userData, default)
        );

        Assert.Equal(HttpStatusCode.Conflict, ex.Code);
    }

    [Fact]
    public async Task Expect_Edit_User_Same_Username_Does_Not_Throw()
    {
        await SendAsync(new RegisterUserData("user5", "user5@example.com", "password5"));

        var userData = new EditUserData();
        userData.Username = "user5";

        var result = await CreateHandling("user5").Update(userData, default);

        Assert.Equal("user5", result.User.Username);
    }

    [Fact]
    public async Task Expect_Edit_User_Same_Email_Does_Not_Throw()
    {
        await SendAsync(new RegisterUserData("user6", "user6@example.com", "password6"));

        var userData = new EditUserData();
        userData.Email = "user6@example.com";

        var result = await CreateHandling("user6").Update(userData, default);

        Assert.Equal("user6@example.com", result.User.Email);
    }
}
