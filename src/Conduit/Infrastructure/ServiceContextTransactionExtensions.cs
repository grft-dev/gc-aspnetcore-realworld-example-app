using System;
using System.Threading;
using System.Threading.Tasks;
using Graftcode.Pipeline;

namespace Conduit.Infrastructure;

public static class ServiceContextTransactionExtensions
{
    public static Task<T> RunInTransactionAsync<T>(
        this ServiceContext context,
        Func<Task<T>> operation
    ) => context.RunInTransactionAsync(operation, CancellationToken.None);

    public static async Task<T> RunInTransactionAsync<T>(
        this ServiceContext context,
        Func<Task<T>> operation,
        CancellationToken cancellationToken
    )
    {
        var database = context.GetRequiredService<ConduitContext>();
        try
        {
            await database.BeginTransactionAsync(cancellationToken);
            var result = await operation();
            await database.CommitTransactionAsync(cancellationToken);
            return result;
        }
        catch
        {
            await database.RollbackTransactionAsync();
            throw;
        }
    }

    public static Task RunInTransactionAsync(this ServiceContext context, Func<Task> operation) =>
        context.RunInTransactionAsync(operation, CancellationToken.None);

    public static async Task RunInTransactionAsync(
        this ServiceContext context,
        Func<Task> operation,
        CancellationToken cancellationToken
    )
    {
        var database = context.GetRequiredService<ConduitContext>();
        try
        {
            await database.BeginTransactionAsync(cancellationToken);
            await operation();
            await database.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await database.RollbackTransactionAsync();
            throw;
        }
    }
}
