namespace AgoraAgentBackend.Domain.DTOs
{
    public class TriggerTradeRequestDto
    {
        public string? ApiKey { get; set; }
        public decimal TradeAmount { get; set; } = 0.01m;
        public string? CustomPrompt { get; set; }
    }
}
