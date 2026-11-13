using System.Threading;
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

    public static Task<UserEnvelope> Register(string username, string email, string password) =>
        ServicePipeline.RunAsync(
            nameof(UsersService),
            nameof(Register),
            async ctx =>
            {
                var handling = ctx.GetRequiredService<UsersHandling>();
                return await ctx.RunInTransactionAsync(
                    () =>
                        handling.Register(
                            new Create.UserData(username, email, password),
                            CancellationToken.None
                        ),
                    CancellationToken.None
                );
            },
            ("username", username),
            ("email", email),
            ("password", password)
        );

    public static Task<UserEnvelope> Login(string email, string password) =>
        ServicePipeline.RunAsync(
            nameof(UsersService),
            nameof(Login),
            async ctx =>
            {
                var handling = ctx.GetRequiredService<UsersHandling>();
                return await ctx.RunInTransactionAsync(
                    () =>
                        handling.Login(
                            new Login.Command(
                                new Login.UserData { Email = email, Password = password }
                            ),
                            CancellationToken.None
                        ),
                    CancellationToken.None
                );
            },
            ("email", email),
            ("password", password)
        );
}
