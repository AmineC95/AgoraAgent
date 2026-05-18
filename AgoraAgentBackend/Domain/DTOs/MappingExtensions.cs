using AgoraAgentBackend.Domain.Entities;
using AgoraAgentBackend.Domain.Enums;

namespace AgoraAgentBackend.Domain.DTOs;

public static class MappingExtensions
{
    public static AgentDto ToDto(this Agent agent)
    {
        return new AgentDto
        {
            Id = agent.Id,
            PublicAddress = agent.WalletAddress,
            BondBalance = agent.BondBalance,
            Status = agent.CurrentStatus.ToString()
        };
    }

    public static TradingTransactionDto ToDto(this TradingTransaction tx)
    {
        return new TradingTransactionDto
        {
            Id = tx.Id,
            AgentId = tx.AgentId,
            TxHash = tx.TxHash,
            Action = tx.Action.ToString(),
            Amount = tx.Amount,
            PriceAtTrade = tx.PriceAtTrade,
            Status = tx.Status.ToString(),
            FailureReason = tx.FailureReason,
            CreatedAt = tx.CreatedAt
        };
    }
}
