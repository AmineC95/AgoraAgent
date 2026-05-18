using System;
using System.ComponentModel.DataAnnotations;

namespace AgoraAgentBackend.Domain.Entities;

public class ProcessedWebhook
{
    [Key]
    public Guid Id { get; private set; }

    [Required]
    public string MessageId { get; private set; }

    public DateTime ProcessedAt { get; private set; }

    public ProcessedWebhook(Guid id, string messageId, DateTime processedAt)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        MessageId = messageId ?? throw new ArgumentNullException(nameof(messageId));
        ProcessedAt = processedAt;
    }

    private ProcessedWebhook() { MessageId = string.Empty; ProcessedAt = DateTime.MinValue; }
}
