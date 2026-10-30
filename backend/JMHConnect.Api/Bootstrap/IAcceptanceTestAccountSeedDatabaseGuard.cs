using JMHConnect.Api.Data;

namespace JMHConnect.Api.Bootstrap;

public interface IAcceptanceTestAccountSeedDatabaseGuard
{
    Task<IFirstPastorBootstrapTransaction> AcquireVerifiedTransactionAsync(
        ApplicationDbContext db,
        AcceptanceTestAccountSeedRequest request,
        CancellationToken cancellationToken);
}
