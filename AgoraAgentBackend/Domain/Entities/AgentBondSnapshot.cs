using System;
using System.ComponentModel.DataAnnotations;

namespace AgoraAgentBackend.Domain.Entities;

public class AgentBondSnapshot
{
    [Key]
    public Guid Id { get; private set; }

    [Required]
    public Guid AgentId { get; private set; }

    public decimal Balance { get; private set; }

    public DateTime SnapshotAt { get; private set; }

    public AgentBondSnapshot(Guid id, Guid agentId, decimal balance, DateTime snapshotAt)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        AgentId = agentId;
        Balance = balance;
        SnapshotAt = snapshotAt;
    }

    private AgentBondSnapshot() { Balance = 0m; SnapshotAt = DateTime.MinValue; }

    public void Update(decimal newBalance)
    {
        Balance = newBalance;
        SnapshotAt = DateTime.UtcNow;
    }
}
