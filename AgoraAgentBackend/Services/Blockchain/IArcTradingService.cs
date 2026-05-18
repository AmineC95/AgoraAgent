using System;
using System.Threading;
using System.Threading.Tasks;
using AgoraAgentBackend.Domain.Enums;
using Nethereum.RPC.Eth.DTOs;

namespace AgoraAgentBackend.Services.Blockchain;

public interface IArcTradingService
{
    Task ExecuteTradeAsync(Guid agentId, TradeAction action, decimal usdcAmount, CancellationToken cancellationToken = default);

    Task<(decimal NativeBalance, decimal UsdcBalance)> GetAgentOnChainBalancesAsync(Guid agentId, CancellationToken cancellationToken = default);

    Task<(int SuccessCount, int FailedCount, decimal TotalBond)> GetTradingStatsAsync(Guid? agentId = null, CancellationToken cancellationToken = default);

    Task<TransactionReceipt?> GetTransactionReceiptAsync(string txHash, CancellationToken cancellationToken = default);

    Task<string?> GetTransactionRevertReasonAsync(string txHash, CancellationToken cancellationToken = default);
}
