using Enakliyat.Domain;

namespace Enakliyat.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly EnakliyatDbContext _context;

    public IRepository<MoveRequest> MoveRequests { get; }

    public UnitOfWork(EnakliyatDbContext context)
    {
        _context = context;
        MoveRequests = new EfRepository<MoveRequest>(_context);
    }

    public async Task<int> CommitAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }
}
