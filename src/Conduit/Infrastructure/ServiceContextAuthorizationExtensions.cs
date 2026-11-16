using System;
using System.Threading.Tasks;
using Graftcode.Pipeline;

namespace Conduit.Infrastructure;

public static class ServiceContextAuthorizationExtensions
{
    public static async Task<T> RunAuthorizedAsync<T>(
        this ServiceContext context,
        string? token,
        Func<Task<T>> operation
    )
    {
        using (GraftUser.Begin(context, await GraftUser.Authenticate(context, token)))
        {
            return await operation();
        }
    }

    public static async Task RunAuthorizedAsync(
        this ServiceContext context,
        string? token,
        Func<Task> operation
    )
    {
        using (GraftUser.Begin(context, await GraftUser.Authenticate(context, token)))
        {
            await operation();
        }
    }

    public static Task<T> RunAuthorizedTransactionAsync<T>(
        this ServiceContext context,
        string? token,
        Func<Task<T>> operation
    ) => context.RunAuthorizedAsync(token, () => context.RunInTransactionAsync(operation));

    public static Task RunAuthorizedTransactionAsync(
        this ServiceContext context,
        string? token,
        Func<Task> operation
    ) => context.RunAuthorizedAsync(token, () => context.RunInTransactionAsync(operation));
}
