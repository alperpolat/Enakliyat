using Enakliyat.Domain;

namespace Enakliyat.Infrastructure.Repositories;

public interface IUnitOfWork : IAsyncDisposable
{
    IRepository<MoveRequest> MoveRequests { get; }
    Task<int> CommitAsync(CancellationToken cancellationToken = default);
}
