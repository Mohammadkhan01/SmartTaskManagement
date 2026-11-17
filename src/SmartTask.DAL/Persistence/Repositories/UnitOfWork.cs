using System.Data;
using Microsoft.EntityFrameworkCore;
using SmartTask.BLL.Interfaces.Repositories;

namespace SmartTask.DAL.Persistence.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly SmartTaskDbContext _context;

    public UnitOfWork(SmartTaskDbContext context)
    {
        _context = context;
    }


    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        try
        {
            await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
