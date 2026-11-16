using System.Net;
using System.Threading.Tasks;
using Conduit;
using Conduit.Infrastructure;
using Conduit.Infrastructure.Errors;
using Graftcode.Pipeline;

namespace Conduit.Features.Users;

public static class UserService
{
    static UserService()
    {
        ServicePipeline.Use(() => GraftHost.Services);
    }

    public static Task<User> GetCurrent(string token) =>
        ServicePipeline.RunAsync(
            nameof(UserService),
            nameof(GetCurrent),
            ctx =>
                ctx.RunAuthorizedAsync(
                    token,
                    async () =>
                    {
                        var personId = ctx.GetRequiredService<ICurrentUserAccessor>()
                            .GetCurrentPersonId();
                        if (personId is null)
                        {
                            throw new RestException(
                                HttpStatusCode.Unauthorized,
                                "token",
                                "is missing"
                            );
                        }

                        var handling = ctx.GetRequiredService<UserHandling>();
                        return await handling.Get(new Details.Query(personId.Value), default);
                    }
                ),
            ("token", token)
        );

    public static Task<User> Update(
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
            ctx =>
                ctx.RunAuthorizedTransactionAsync(
                    token,
                    async () =>
                    {
                        var user = new EditUserData();
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

                        var handling = ctx.GetRequiredService<UserHandling>();
                        return await handling.Update(user, default);
                    }
                ),
            ("token", token),
            ("username", username),
            ("email", email),
            ("password", password),
            ("bio", bio),
            ("image", image)
        );
}
