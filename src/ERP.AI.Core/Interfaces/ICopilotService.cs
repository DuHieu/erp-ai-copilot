using ERP.AI.Core.Dtos;

namespace ERP.AI.Core.Interfaces;

public interface ICopilotService
{
    Task<CopilotChatResponse> ProcessMessageAsync(CopilotChatRequest request, CancellationToken cancellationToken = default);
}
