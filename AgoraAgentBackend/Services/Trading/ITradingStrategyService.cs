using System;
using System.Threading;
using System.Threading.Tasks;

namespace AgoraAgentBackend.Services.Trading;

public interface ITradingStrategyService
{
    Task<bool> ShouldTradeAsync(Guid agentId, CancellationToken cancellationToken = default);

    Task MarkTradeExecutedAsync(Guid agentId, CancellationToken cancellationToken = default);
}
