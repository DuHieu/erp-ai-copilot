using ERP.AI.Core.Dtos;
using ERP.AI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ERP.AI.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CopilotController : ControllerBase
{
    private readonly ICopilotService _copilotService;
    private readonly ILogger<CopilotController> _logger;

    public CopilotController(ICopilotService copilotService, ILogger<CopilotController> logger)
    {
        _copilotService = copilotService;
        _logger = logger;
    }

    /// <summary>
    /// Processes a natural language question about ERP business data using safe tool calling.
    /// </summary>
    /// <param name="request">User chat message payload</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Copilot answer, tools executed, structured data, and trace timing metrics.</returns>
    [HttpPost("chat")]
    [ProducesResponseType(typeof(CopilotChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CopilotChatResponse>> Chat(
        [FromBody] CopilotChatRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { error = "Message payload cannot be empty." });
        }

        _logger.LogInformation("Received ERP AI query: {Question}", request.Message);

        var response = await _copilotService.ProcessMessageAsync(request, cancellationToken);
        return Ok(response);
    }
}
