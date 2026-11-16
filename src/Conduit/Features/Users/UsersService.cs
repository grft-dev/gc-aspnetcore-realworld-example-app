using System.Threading.Tasks;
using Conduit;
using Conduit.Infrastructure;
using Graftcode.Pipeline;

namespace Conduit.Features.Users;

public static class UsersService
{
    static UsersService()
    {
        ServicePipeline.Use(() => GraftHost.Services);
    }

    public static Task<User> Register(string username, string email, string password) =>
        ServicePipeline.RunAsync(
            nameof(UsersService),
            nameof(Register),
            async ctx =>
            {
                var handling = ctx.GetRequiredService<UsersHandling>();
                return await ctx.RunInTransactionAsync(
                    () => handling.Register(new RegisterUserData(username, email, password), default)
                );
            },
            ("username", username),
            ("email", email),
            ("password", password)
        );

    public static Task<User> Login(string email, string password) =>
        ServicePipeline.RunAsync(
            nameof(UsersService),
            nameof(Login),
            async ctx =>
            {
                var handling = ctx.GetRequiredService<UsersHandling>();
                return await ctx.RunInTransactionAsync(
                    () =>
                        handling.Login(
                            new Login.Command(new Login.UserData { Email = email, Password = password }),
                            default
                        )
                );
            },
            ("email", email),
            ("password", password)
        );
}
