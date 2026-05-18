using System;
using System.ComponentModel.DataAnnotations;
using AgoraAgentBackend.Domain.Enums;
using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgoraAgentBackend.Domain.Entities;

public class Agent
{
    [Key]
    public Guid Id { get; private set; }

    [Required]
    public string Name { get; private set; }

    [Required]
    public string WalletAddress { get; private set; }

    [JsonIgnore]
    [NotMapped]
    public string? PrivateKey { get; private set; }

    public decimal BondBalance { get; private set; }

    public AgentStatus CurrentStatus { get; private set; }

    public Agent(Guid id, string name, string walletAddress, decimal bondBalance, AgentStatus currentStatus)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        WalletAddress = walletAddress ?? throw new ArgumentNullException(nameof(walletAddress));
        BondBalance = bondBalance;
        CurrentStatus = currentStatus;
    }

    private Agent() { Name = string.Empty; WalletAddress = string.Empty; CurrentStatus = AgentStatus.Active; }

    public void CreditBond(decimal amount)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        BondBalance += amount;
    }

    public void DebitBond(decimal amount)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (amount > BondBalance) throw new InvalidOperationException("Insufficient bond balance");
        BondBalance -= amount;
    }

    public void MarkSlashed() => CurrentStatus = AgentStatus.Slashed;
}
