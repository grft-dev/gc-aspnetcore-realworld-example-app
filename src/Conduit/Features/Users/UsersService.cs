using System.Threading;
using System.Threading.Tasks;
using Conduit;
using Graftcode.Pipeline;
using Mediator;
using Microsoft.Extensions.DependencyInjection;

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
                await ctx.GetRequiredService<IMediator>()
                    .Send(
                        new Create.Command(new Create.UserData(username, email, password)),
                        CancellationToken.None
                    ),
            ("username", username),
            ("email", email),
            ("password", password)
        );

    public static Task<UserEnvelope> Login(string email, string password) =>
        ServicePipeline.RunAsync(
            nameof(UsersService),
            nameof(Login),
            async ctx =>
                await ctx.GetRequiredService<IMediator>()
                    .Send(
                        new Login.Command(
                            new Login.UserData { Email = email, Password = password }
                        ),
                        CancellationToken.None
                    ),
            ("email", email),
            ("password", password)
        );
}
