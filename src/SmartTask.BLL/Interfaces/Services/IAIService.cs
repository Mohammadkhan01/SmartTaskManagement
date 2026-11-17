using SmartTask.BLL.DTOs.AI;

namespace SmartTask.BLL.Interfaces.Services;

public interface IAIService
{
    Task<EnhanceTaskDescriptionResponse> EnhanceTaskDescriptionAsync(
        EnhanceTaskDescriptionRequest request,
        CancellationToken cancellationToken = default);
}
