using System;
using System.Threading.Tasks;
using AgoraAgentBackend.Data;
using AgoraAgentBackend.Domain.DTOs;
using AgoraAgentBackend.Domain.Entities;
using AgoraAgentBackend.Domain.Enums;
using AgoraAgentBackend.Services.Infrastructure;
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

    public DemoController(ApplicationDbContext db, IHubContext<TradeHub> hub)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
    }

    [HttpPost("trigger-trade")]
    public async Task<IActionResult> TriggerTrade()
    {
        // Get first agent if any, otherwise mock a GUID
        var agent = await _db.Agents.OrderBy(a => a.Name).FirstOrDefaultAsync().ConfigureAwait(false);
        var agentId = agent != null ? agent.Id : Guid.NewGuid();

        // Create a simulated trading transaction
        var txHash = "SIMULATED_" + Guid.NewGuid().ToString("N");
        var tx = new TradingTransaction(Guid.Empty, agentId, txHash, TradeAction.Buy, 150m, 1m);

        _db.TradingTransactions.Add(tx);
        await _db.SaveChangesAsync().ConfigureAwait(false);

        // Broadcast initial pending transaction
        await _hub.Clients.All.SendAsync("TradeUpdated", tx.ToDto()).ConfigureAwait(false);

        // Simulate network validation delay
        await Task.Delay(2500).ConfigureAwait(false);

        // Mark success and broadcast update
        tx.MarkSuccess();
        _db.TradingTransactions.Update(tx);
        await _db.SaveChangesAsync().ConfigureAwait(false);

        await _hub.Clients.All.SendAsync("TradeUpdated", tx.ToDto()).ConfigureAwait(false);

        return Ok(tx.ToDto());
    }
}
