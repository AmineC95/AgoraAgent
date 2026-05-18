using System;

namespace AgoraAgentBackend.Domain.DTOs;

public class TradingTransactionDto
{
    public Guid Id { get; set; }

    public Guid AgentId { get; set; }

    public string TxHash { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public decimal PriceAtTrade { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? FailureReason { get; set; }

    public DateTime CreatedAt { get; set; }
}
