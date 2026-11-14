using SmartTask.BLL.Interfaces.Repositories;

namespace SmartTask.DAL.Persistence.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly SmartTaskDbContext _context;

    public UnitOfWork(SmartTaskDbContext context)
    {
        _context = context;
    }

    public Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
