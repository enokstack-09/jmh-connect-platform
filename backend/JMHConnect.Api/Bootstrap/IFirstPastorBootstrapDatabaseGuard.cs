using JMHConnect.Api.Data;
using Microsoft.EntityFrameworkCore.Storage;

namespace JMHConnect.Api.Bootstrap;

public interface IFirstPastorBootstrapDatabaseGuard
{
    Task<IFirstPastorBootstrapTransaction> AcquireVerifiedTransactionAsync(
        ApplicationDbContext db,
        string expectedDatabaseName,
        CancellationToken cancellationToken);
}

public interface IFirstPastorBootstrapTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}

public sealed class EfFirstPastorBootstrapTransaction(IDbContextTransaction transaction)
    : IFirstPastorBootstrapTransaction
{
    public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);

    public ValueTask DisposeAsync() => transaction.DisposeAsync();
}
