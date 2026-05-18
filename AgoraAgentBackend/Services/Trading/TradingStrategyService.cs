using System;
using System.Threading;
using System.Threading.Tasks;
using AgoraAgentBackend.Data;
using Microsoft.EntityFrameworkCore;

namespace AgoraAgentBackend.Services.Trading;

public class TradingStrategyService : ITradingStrategyService
{
    private readonly ApplicationDbContext _db;

    public TradingStrategyService(ApplicationDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<bool> ShouldTradeAsync(Guid agentId, CancellationToken cancellationToken = default)
    {
        var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == agentId, cancellationToken).ConfigureAwait(false);
        if (agent == null) return false;

        var snapshot = await _db.AgentBondSnapshots.FirstOrDefaultAsync(s => s.AgentId == agentId, cancellationToken).ConfigureAwait(false);
        if (snapshot == null)
        {
            // Initialize snapshot and don't trade on first observation
            _db.AgentBondSnapshots.Add(new Domain.Entities.AgentBondSnapshot(Guid.Empty, agentId, agent.BondBalance, DateTime.UtcNow));
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        if (snapshot.Balance <= 0m)
        {
            // Avoid divide by zero; require absolute growth
            return agent.BondBalance > 0m;
        }

        var increase = agent.BondBalance - snapshot.Balance;
        var pct = increase / snapshot.Balance;
        return pct > 0.10m;
    }

    public async Task MarkTradeExecutedAsync(Guid agentId, CancellationToken cancellationToken = default)
    {
        var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == agentId, cancellationToken).ConfigureAwait(false);
        if (agent == null) return;

        var snapshot = await _db.AgentBondSnapshots.FirstOrDefaultAsync(s => s.AgentId == agentId, cancellationToken).ConfigureAwait(false);
        if (snapshot == null)
        {
            _db.AgentBondSnapshots.Add(new Domain.Entities.AgentBondSnapshot(Guid.Empty, agentId, agent.BondBalance, DateTime.UtcNow));
        }
        else
        {
            snapshot.Update(agent.BondBalance);
            _db.AgentBondSnapshots.Update(snapshot);
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
