using System;
using System.ComponentModel.DataAnnotations;

namespace AgoraAgentBackend.Domain.Entities;

public class UserFollower
{
    [Key]
    public Guid Id { get; private set; }

    [Required]
    public string WalletAddress { get; private set; }

    [Required]
    public Guid AgentId { get; private set; }

    public decimal AmountStaked { get; private set; }

    public UserFollower(Guid id, string walletAddress, Guid agentId, decimal amountStaked)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        WalletAddress = walletAddress ?? throw new ArgumentNullException(nameof(walletAddress));
        AgentId = agentId;
        AmountStaked = amountStaked;
    }

    private UserFollower() { WalletAddress = string.Empty; }

    public void Stake(decimal amount)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        AmountStaked += amount;
    }

    public void Unstake(decimal amount)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (amount > AmountStaked) throw new InvalidOperationException("Insufficient staked amount");
        AmountStaked -= amount;
    }
}
