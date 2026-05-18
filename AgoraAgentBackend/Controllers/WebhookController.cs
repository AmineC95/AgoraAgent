using System;
using System.Threading.Tasks;
using AgoraAgentBackend.Services.Infrastructure;
using AgoraAgentBackend.Data;
using AgoraAgentBackend.Domain.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text;
using System.Text.Json;
using AgoraAgentBackend.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgoraAgentBackend.Controllers;

[ApiController]
[Route("api/webhooks/circle")]
public class WebhookController : ControllerBase
{
    private readonly ICircleWebhookService _circleWebhookService;
    private readonly ILogger<WebhookController> _logger;
    private readonly ApplicationDbContext _db;

    public WebhookController(ICircleWebhookService circleWebhookService, ILogger<WebhookController> logger, ApplicationDbContext db)
    {
        _circleWebhookService = circleWebhookService;
        _logger = logger;
        _db = db;
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(typeof(AgentDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Post()
    {
        try
        {
            var valid = await _circleWebhookService.HandleAsync(Request).ConfigureAwait(false);
            if (!valid) return Unauthorized();
            // Try to extract agent id or wallet from body to return AgentDto
            try
            {
                Request.EnableBuffering();
                using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
                var body = await reader.ReadToEndAsync().ConfigureAwait(false);
                Request.Body.Position = 0;

                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                Guid? agentId = null;
                if (TryGetString(root, "agentId", out var aid) && Guid.TryParse(aid, out var g)) agentId = g;
                if (root.TryGetProperty("data", out var data))
                {
                    if (TryGetString(data, "agentId", out var da) && Guid.TryParse(da, out var g2)) agentId ??= g2;
                }
                if (root.TryGetProperty("payload", out var payload))
                {
                    if (TryGetString(payload, "agentId", out var pa) && Guid.TryParse(pa, out var g3)) agentId ??= g3;
                }

                Agent? agent = null;
                if (agentId.HasValue)
                {
                    agent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == agentId.Value).ConfigureAwait(false);
                }

                if (agent == null && TryGetString(root, "walletAddress", out var w))
                {
                    agent = await _db.Agents.FirstOrDefaultAsync(a => a.WalletAddress == w).ConfigureAwait(false);
                }

                if (agent == null) return Ok();
                return Ok(agent.ToDto());
            }
            catch
            {
                return Ok();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in webhook endpoint");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    private bool TryGetString(JsonElement element, string name, out string? value)
    {
        value = null;
        if (element.ValueKind != JsonValueKind.Object) return false;
        if (!element.TryGetProperty(name, out var prop)) return false;
        if (prop.ValueKind == JsonValueKind.String)
        {
            value = prop.GetString();
            return true;
        }

        value = prop.ToString();
        return true;
    }
}
