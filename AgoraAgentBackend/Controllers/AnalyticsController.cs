using System;
using System.Threading.Tasks;
using AgoraAgentBackend.Data;
using AgoraAgentBackend.Domain.DTOs;
using AgoraAgentBackend.Services.Blockchain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace AgoraAgentBackend.Controllers;

[ApiController]
[Route("api/analytics")]
public class AnalyticsController : ControllerBase
{
    private readonly IArcTradingService _arcTradingService;
    private readonly ApplicationDbContext _db;

    public AnalyticsController(IArcTradingService arcTradingService, ApplicationDbContext db)
    {
        _arcTradingService = arcTradingService;
        _db = db;
    }

    [HttpGet("status")]
    [ProducesResponseType(typeof(AgentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatus([FromQuery] Guid? agentId = null)
    {
        if (agentId.HasValue)
        {
            var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == agentId.Value).ConfigureAwait(false);
            if (agent == null) return NotFound();
            return Ok(agent.ToDto());
        }

        var list = await _db.Agents
            .OrderBy(a => a.Name)
            .Select(a => new AgentDto { Id = a.Id, PublicAddress = a.WalletAddress, BondBalance = a.BondBalance, Status = a.CurrentStatus.ToString() })
            .ToListAsync()
            .ConfigureAwait(false);

        return Ok(list);
    }

    [HttpGet("performance")]
    [ProducesResponseType(typeof(IEnumerable<TradingTransactionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPerformanceHistory([FromQuery] Guid? agentId = null, [FromQuery] int days = 30)
    {
        var since = DateTime.UtcNow.AddDays(-Math.Max(1, days));

        var query = _db.TradingTransactions.AsQueryable();
        if (agentId.HasValue) query = query.Where(t => t.AgentId == agentId.Value);

        var list = await query
            .Where(t => t.CreatedAt >= since)
            .OrderBy(t => t.CreatedAt)
            .Select(t => new TradingTransactionDto
            {
                Id = t.Id,
                AgentId = t.AgentId,
                TxHash = t.TxHash,
                Action = t.Action.ToString(),
                Amount = t.Amount,
                PriceAtTrade = t.PriceAtTrade,
                Status = t.Status.ToString(),
                FailureReason = t.FailureReason,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync()
            .ConfigureAwait(false);

        return Ok(list);
    }
}
