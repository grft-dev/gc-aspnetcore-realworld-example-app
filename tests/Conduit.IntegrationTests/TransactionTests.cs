using System;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Domain;
using Conduit.Features.Articles;
using Conduit.Infrastructure;
using Graftcode.Pipeline;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Conduit.IntegrationTests;

public class TransactionTests : SliceFixture
{
    [Fact]
    public async Task Queries_Do_Not_Start_A_Transaction()
    {
        var db = GetDbContext();
        var handling = new ArticlesHandling(db, new StubCurrentUserAccessor("nobody"));
        await handling.List(new List.Query(null, null, null, null, null), CancellationToken.None);
        Assert.Null(db.Database.CurrentTransaction);
    }

    [Fact]
    public async Task Failed_Commands_Roll_Back_Partial_Writes()
    {
        await ExecuteScopeAsync(async services =>
        {
            var db = services.GetRequiredService<ConduitContext>();
            var serviceContext = new ServiceContext("TransactionTests", "Rollback", services, []);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                serviceContext.RunInTransactionAsync(
                    async () =>
                    {
                        Assert.NotNull(db.Database.CurrentTransaction);
                        db.Add(new Person { Username = "partial" });
                        await db.SaveChangesAsync(CancellationToken.None);
                        throw new InvalidOperationException("Failure after the first save.");
                    },
                    CancellationToken.None
                )
            );

            Assert.Null(db.Database.CurrentTransaction);
            Assert.Equal(0, await db.Persons.CountAsync());
        });
    }
}
