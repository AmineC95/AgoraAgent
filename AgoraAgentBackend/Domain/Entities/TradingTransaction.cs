using System;
using System.ComponentModel.DataAnnotations;
using AgoraAgentBackend.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgoraAgentBackend.Domain.Entities;

public class TradingTransaction
{
    [Key]
    public Guid Id { get; private set; }

    [Required]
    public Guid AgentId { get; private set; }

    [Required]
    public string TxHash { get; private set; }

    public TradeAction Action { get; private set; }

    public decimal Amount { get; private set; }

    public decimal PriceAtTrade { get; private set; }

    public TradingStatus Status { get; private set; } = TradingStatus.Pending;

    public string? FailureReason { get; private set; }
    
    public DateTime CreatedAt { get; private set; }

    public TradingTransaction(Guid id, Guid agentId, string txHash, TradeAction action, decimal amount, decimal priceAtTrade)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        AgentId = agentId;
        TxHash = txHash ?? throw new ArgumentNullException(nameof(txHash));
        Action = action;
        Amount = amount;
        PriceAtTrade = priceAtTrade;
        Status = TradingStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    private TradingTransaction() { TxHash = string.Empty; }

    public void MarkSuccess()
    {
        Status = TradingStatus.Success;
        FailureReason = null;
    }

    public void MarkFailed(string reason)
    {
        Status = TradingStatus.Failed;
        FailureReason = reason;
    }
}
