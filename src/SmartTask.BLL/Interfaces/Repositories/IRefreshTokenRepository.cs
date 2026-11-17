using SmartTask.Domain.Entities;

namespace SmartTask.BLL.Interfaces.Repositories;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RefreshToken>> GetActiveByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default);

    void Add(RefreshToken refreshToken);
}
