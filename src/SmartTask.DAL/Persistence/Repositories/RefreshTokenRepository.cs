using Microsoft.EntityFrameworkCore;
using SmartTask.BLL.Interfaces.Repositories;
using SmartTask.Domain.Entities;

namespace SmartTask.DAL.Persistence.Repositories;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly SmartTaskDbContext _context;

    public RefreshTokenRepository(SmartTaskDbContext context)
    {
        _context = context;
    }

    public Task<RefreshToken?> GetByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        return _context.RefreshTokens
            .FirstOrDefaultAsync(
                token => token.TokenHash == tokenHash,
                cancellationToken);
    }

    public async Task<IReadOnlyList<RefreshToken>> GetActiveByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        return await _context.RefreshTokens
            .Where(token =>
                token.UserId == userId &&
                token.RevokedAtUtc == null &&
                token.ExpiresAtUtc > now)
            .ToListAsync(cancellationToken);
    }

    public void Add(RefreshToken refreshToken)
    {
        _context.RefreshTokens.Add(refreshToken);
    }
}
