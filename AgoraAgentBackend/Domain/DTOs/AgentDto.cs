using System;

namespace AgoraAgentBackend.Domain.DTOs;

public class AgentDto
{
    public Guid Id { get; set; }

    public string PublicAddress { get; set; } = string.Empty;

    public decimal BondBalance { get; set; }

    public string Status { get; set; } = string.Empty;
}
