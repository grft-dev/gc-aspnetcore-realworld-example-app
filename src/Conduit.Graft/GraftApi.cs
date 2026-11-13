using System.Threading.Tasks;
using Conduit;
using Graftcode.Pipeline;

namespace Conduit.Graft;

public static class UsersService
{
    static UsersService() => ServicePipeline.Use(() => GraftHost.Services);

    public static Task<string> Register(string username, string email, string password) =>
        ServicePipeline.RunAsync(
            nameof(UsersService),
            nameof(Register),
            async _ =>
                (await Conduit.Features.Users.UsersService.Register(username, email, password))
                    .User
                    .Token
                ?? string.Empty,
            ("username", username),
            ("email", email),
            ("password", password)
        );

    public static Task<string> Login(string email, string password) =>
        ServicePipeline.RunAsync(
            nameof(UsersService),
            nameof(Login),
            async _ =>
                (await Conduit.Features.Users.UsersService.Login(email, password)).User.Token
                ?? string.Empty,
            ("email", email),
            ("password", password)
        );
}

public static class ArticlesService
{
    static ArticlesService() => ServicePipeline.Use(() => GraftHost.Services);

    public static Task<string> Create(
        string token,
        string title,
        string description,
        string body
    ) =>
        ServicePipeline.RunAsync(
            nameof(ArticlesService),
            nameof(Create),
            async _ =>
                (
                    await Conduit.Features.Articles.ArticlesService.Create(
                        token,
                        title,
                        description,
                        body,
                        null
                    )
                ).Slug
                ?? string.Empty,
            ("token", token),
            ("title", title),
            ("description", description),
            ("body", body)
        );

    public static Task<string> Get(string token, string slug) =>
        ServicePipeline.RunAsync(
            nameof(ArticlesService),
            nameof(Get),
            async _ =>
                (await Conduit.Features.Articles.ArticlesService.Get(token, slug)).Title
                ?? string.Empty,
            ("token", token),
            ("slug", slug)
        );
}
