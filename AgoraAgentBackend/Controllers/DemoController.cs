using System;
using System.Threading.Tasks;
using AgoraAgentBackend.Data;
using AgoraAgentBackend.Domain.DTOs;
using AgoraAgentBackend.Domain.Entities;
using AgoraAgentBackend.Domain.Enums;
using AgoraAgentBackend.Services.Infrastructure;
using AgoraAgentBackend.Services.Blockchain;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AgoraAgentBackend.Controllers;

[ApiController]
[Route("api/demo")]
public class DemoController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<TradeHub> _hub;
    private readonly IArcTradingService _arcTradingService;

    public DemoController(ApplicationDbContext db, IHubContext<TradeHub> hub, IArcTradingService arcTradingService)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
        _arcTradingService = arcTradingService ?? throw new ArgumentNullException(nameof(arcTradingService));
    }

    [HttpPost("trigger-trade")]
    public async Task<IActionResult> TriggerTrade()
    {
        var agent = await _db.Agents.OrderBy(a => a.Name).FirstOrDefaultAsync().ConfigureAwait(false);
        if (agent == null) return BadRequest("No agent configured to execute on-chain trade");

        // Execute a real on-chain native transfer (0.01 ETH)
        await _arcTradingService.ExecuteTradeAsync(agent.Id, TradeAction.Buy, 0.01m).ConfigureAwait(false);

        // Return the latest transaction for the agent
        var lastTx = await _db.TradingTransactions
            .Where(t => t.AgentId == agent.Id)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync().ConfigureAwait(false);

        if (lastTx != null)
        {
            try { await _hub.Clients.All.SendAsync("TradeUpdated", lastTx.ToDto()).ConfigureAwait(false); } catch { }
            return Ok(lastTx.ToDto());
        }

        return Ok();
    }
}
