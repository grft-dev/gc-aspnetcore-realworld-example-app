using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Conduit;
using Conduit.Infrastructure;
using Conduit.Infrastructure.Errors;
using Graftcode.Pipeline;
using Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace Conduit.Features.Users;

public static class UserService
{
    static UserService()
    {
        ServicePipeline.Use(() => GraftHost.Services);
    }

    public static Task<UserEnvelope> GetCurrent(string token) =>
        ServicePipeline.RunAsync(
            nameof(UserService),
            nameof(GetCurrent),
            async ctx =>
            {
                using (GraftUser.Begin(ctx, await GraftUser.Authenticate(ctx, token)))
                {
                    var personId = ctx.GetRequiredService<ICurrentUserAccessor>()
                        .GetCurrentPersonId();
                    if (personId is null)
                    {
                        throw new RestException(HttpStatusCode.Unauthorized, "token", "is missing");
                    }

                    return await ctx.GetRequiredService<IMediator>()
                        .Send(new Details.Query(personId.Value), CancellationToken.None);
                }
            },
            ("token", token)
        );

    public static Task<UserEnvelope> Update(
        string token,
        string? username,
        string? email,
        string? password,
        string? bio,
        string? image
    ) =>
        ServicePipeline.RunAsync(
            nameof(UserService),
            nameof(Update),
            async ctx =>
            {
                using (GraftUser.Begin(ctx, await GraftUser.Authenticate(ctx, token)))
                {
                    var user = new Edit.UserData();
                    if (username is not null)
                    {
                        user.Username = username;
                    }

                    if (email is not null)
                    {
                        user.Email = email;
                    }

                    if (password is not null)
                    {
                        user.Password = password;
                    }

                    if (bio is not null)
                    {
                        user.Bio = bio;
                    }

                    if (image is not null)
                    {
                        user.Image = image;
                    }

                    return await ctx.GetRequiredService<IMediator>()
                        .Send(new Edit.Command(user), CancellationToken.None);
                }
            },
            ("token", token),
            ("username", username),
            ("email", email),
            ("password", password),
            ("bio", bio),
            ("image", image)
        );
}
