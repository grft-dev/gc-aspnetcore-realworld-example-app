using System;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Conduit.IntegrationTests;

public class SliceFixture : IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ServiceProvider _provider;
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly IServiceScope _directScope;

    public SliceFixture()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddConduit();

        _connection.Open();
        services.AddDbContext<ConduitContext>(options => options.UseSqlite(_connection));

        _provider = services.BuildServiceProvider();

        _directScope = _provider.CreateScope();
        GetDbContext().Database.EnsureCreated();
        _scopeFactory = _provider.GetRequiredService<IServiceScopeFactory>();
    }

    // A dedicated scope supports older tests that construct handlers directly.
    // Database helpers each use a fresh scoped context.
    public ConduitContext GetDbContext() =>
        _directScope.ServiceProvider.GetRequiredService<ConduitContext>();

    public void Dispose()
    {
        _directScope.Dispose();
        _provider.Dispose();
        _connection.Dispose();
    }

    public async Task ExecuteScopeAsync(Func<IServiceProvider, Task> action)
    {
        using var scope = _scopeFactory.CreateScope();
        await action(scope.ServiceProvider);
    }

    public async Task<T> ExecuteScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var scope = _scopeFactory.CreateScope();
        return await action(scope.ServiceProvider);
    }

    public ValueTask<TResponse> SendAsync<TResponse>(object request) =>
        ExecuteValueTaskScopeAsync(async sp =>
        {
            var response = await DispatchAsync(sp, request, CancellationToken.None);
            return response is TResponse typed
                ? typed
                : throw new InvalidOperationException(
                    $"Expected response of type '{typeof(TResponse).FullName}', got '{response?.GetType().FullName ?? "null"}'."
                );
        });

    public ValueTask SendAsync(object request) =>
        ExecuteValueTaskScopeAsync(async sp =>
        {
            await DispatchAsync(sp, request, CancellationToken.None);
        });

    private async ValueTask<T> ExecuteValueTaskScopeAsync<T>(
        Func<IServiceProvider, ValueTask<T>> action
    )
    {
        using var scope = _scopeFactory.CreateScope();
        return await action(scope.ServiceProvider);
    }

    private async ValueTask ExecuteValueTaskScopeAsync(Func<IServiceProvider, ValueTask> action)
    {
        using var scope = _scopeFactory.CreateScope();
        await action(scope.ServiceProvider);
    }

    public Task ExecuteDbContextAsync(Func<ConduitContext, Task> action) =>
        ExecuteScopeAsync(sp => action(sp.GetRequiredService<ConduitContext>()));

    public Task<T> ExecuteDbContextAsync<T>(Func<ConduitContext, Task<T>> action) =>
        ExecuteScopeAsync(sp => action(sp.GetRequiredService<ConduitContext>()));

    public Task InsertAsync(params object[] entities) =>
        ExecuteDbContextAsync(db =>
        {
            foreach (var entity in entities)
            {
                db.Add(entity);
            }
            return db.SaveChangesAsync();
        });

    private static async ValueTask<object?> DispatchAsync(
        IServiceProvider services,
        object request,
        CancellationToken cancellationToken
    ) =>
        request switch
        {
            Conduit.Features.Users.RegisterUserData user => await services
                .GetRequiredService<Conduit.Features.Users.UsersHandling>()
                .Register(user, cancellationToken),
            Conduit.Features.Users.Login.Command command => await services
                .GetRequiredService<Conduit.Features.Users.UsersHandling>()
                .Login(command, cancellationToken),
            Conduit.Features.Users.EditUserData user => await services
                .GetRequiredService<Conduit.Features.Users.UserHandling>()
                .Update(user, cancellationToken),
            Conduit.Features.Users.Details.Query query => await services
                .GetRequiredService<Conduit.Features.Users.UserHandling>()
                .Get(query, cancellationToken),
            Conduit.Features.Profiles.Details.Query query => await services
                .GetRequiredService<Conduit.Features.Profiles.ProfilesHandling>()
                .Get(query, cancellationToken),
            Conduit.Features.Articles.CreateArticleData articleData => await services
                .GetRequiredService<Conduit.Features.Articles.ArticlesHandling>()
                .Create(articleData, cancellationToken),
            Conduit.Features.Articles.Details.Query query => await services
                .GetRequiredService<Conduit.Features.Articles.ArticlesHandling>()
                .Details(query, cancellationToken),
            Conduit.Features.Articles.List.Query query => await services
                .GetRequiredService<Conduit.Features.Articles.ArticlesHandling>()
                .List(query, cancellationToken),
            Conduit.Features.Comments.List.Query query => await services
                .GetRequiredService<Conduit.Features.Comments.CommentsHandling>()
                .List(query, cancellationToken),
            Conduit.Features.Tags.List.Query query => await services
                .GetRequiredService<Conduit.Features.Tags.TagsHandling>()
                .List(query, cancellationToken),
            _ => throw new InvalidOperationException(
                $"Unsupported request type '{request.GetType().FullName}'."
            ),
        };
}
