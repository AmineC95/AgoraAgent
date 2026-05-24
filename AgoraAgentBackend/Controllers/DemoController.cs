using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AgoraAgentBackend.Data;
using AgoraAgentBackend.Domain.DTOs;
using AgoraAgentBackend.Domain.Entities;
using AgoraAgentBackend.Domain.Enums;
using AgoraAgentBackend.Services.Infrastructure;
using AgoraAgentBackend.Services.Blockchain;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgoraAgentBackend.Controllers;

/// <summary>
/// Controller exposing demo endpoints used during development and testing.
/// </summary>
/// <remarks>
/// TriggerTrade: optionally calls an LLM provider to obtain a single-word decision (BUY/SELL/HOLD),
/// executes the mapped on-chain trade via <see cref="AgoraAgentBackend.Services.Blockchain.IArcTradingService"/>,
/// and broadcasts transaction updates to connected WebSocket clients via SignalR.
/// ResetDatabase: development-only helper to wipe and reseed local development data.
/// </remarks>
[ApiController]
[Route("api/demo")]
public class DemoController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<TradeHub> _hub;
    private readonly IArcTradingService _arcTradingService;
    private readonly ILogger<DemoController> _logger;
    private readonly IConfiguration _configuration;

    public DemoController(ApplicationDbContext db, IHubContext<TradeHub> hub, IArcTradingService arcTradingService, ILogger<DemoController> logger, IConfiguration configuration)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
        _arcTradingService = arcTradingService ?? throw new ArgumentNullException(nameof(arcTradingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Trigger a demo trade using an LLM decision.
    /// </summary>
    /// <remarks>
    /// Flow:
    /// 1. If an API key is provided, call the configured LLM endpoint to obtain a single-word decision.
    /// 2. Map the decision to a <see cref="TradeAction"/> and execute the trade through the blockchain service.
    /// 3. On failure, attempt a small fallback buy to ensure the UI receives an update (useful in dev/testnet).
    /// 4. Return and broadcast the most recent transaction for the agent.
    ///
    /// NOTE (Production Architecture): Replace this simple decision flow with a safe, auditable strategy. Do not rely on unverified single-word LLM outputs in production.
    /// </remarks>
    [HttpPost("trigger-trade")]
    public async Task<IActionResult> TriggerTrade([FromBody] TriggerTradeRequestDto? request = null)
    {
        var agent = await _db.Agents.OrderBy(a => a.Name).FirstOrDefaultAsync().ConfigureAwait(false);
        if (agent == null) return BadRequest("No agent configured to execute on-chain trade");

        string? decision = null;
        var apiKey = request?.ApiKey;

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            decision = await GetGroqDecisionAsync(apiKey, request?.CustomPrompt).ConfigureAwait(false);
        }

        var cmd = decision?.Trim().ToUpperInvariant();

        var amountToTrade = (request != null && request.TradeAmount > 0) ? request.TradeAmount : 0.01m;

        try
        {
            if (!string.IsNullOrWhiteSpace(cmd) && cmd.Contains("BUY"))
            {
                await _arcTradingService.ExecuteTradeAsync(agent.Id, TradeAction.Buy, amountToTrade).ConfigureAwait(false);
            }
            else if (!string.IsNullOrWhiteSpace(cmd) && cmd.Contains("SELL"))
            {
                await _arcTradingService.ExecuteTradeAsync(agent.Id, TradeAction.Sell, amountToTrade).ConfigureAwait(false);
            }
            else
            {
                // HOLD or no key provided => perform a small demo trade so UI updates
                await _arcTradingService.ExecuteTradeAsync(agent.Id, TradeAction.Buy, amountToTrade).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ExecuteTradeAsync failed, attempting fallback {Amount} USDC trade", amountToTrade);
            try
            {
                await _arcTradingService.ExecuteTradeAsync(agent.Id, TradeAction.Buy, amountToTrade).ConfigureAwait(false);
            }
            catch (Exception ex2)
            {
                _logger.LogError(ex2, "Fallback trade also failed");
            }
        }

        // Return the latest transaction for the agent
        try
        {
            var lastTx = await _db.TradingTransactions
                .Where(t => t.AgentId == agent.Id)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync().ConfigureAwait(false);

            if (lastTx != null)
            {
                try { await _hub.Clients.All.SendAsync("TradeUpdated", lastTx.ToDto()).ConfigureAwait(false); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to broadcast trade update"); }
                return Ok(lastTx.ToDto());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve or broadcast last transaction");
        }

        return Ok();
    }

    private async Task<string?> GetGroqDecisionAsync(string apiKey, string? customPrompt)
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var model = _configuration.GetValue<string>("Groq:Model") ?? "llama3-8b-8192";
            var prompt = customPrompt ?? "Tu es un agent de trading crypto autonome. Le marché est volatile. Réponds UNIQUEMENT par l'un de ces trois mots : BUY, SELL, ou HOLD. Aucune autre ponctuation ou justification.";

            var payload = new
            {
                model = model,
                messages = new[] { new { role = "system", content = prompt } }
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var endpoint = _configuration.GetValue<string>("Groq:Endpoint") ?? "https://api.groq.com/openai/v1/chat/completions";
            var resp = await http.PostAsync(endpoint, content).ConfigureAwait(false);
            var respBody = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (resp.IsSuccessStatusCode)
            {
                try
                {
                    using var doc = JsonDocument.Parse(respBody);
                    if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
                    {
                        var first = choices[0];
                        if (first.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var contentElem))
                        {
                            return contentElem.GetString();
                        }
                        else if (first.TryGetProperty("text", out var textElem))
                        {
                            return textElem.GetString();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to parse Groq response: {Response}", respBody);
                }
            }
            else
            {
                _logger.LogWarning("Groq API returned non-success {Status}: {Body}", resp.StatusCode, respBody);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Groq API");
        }

        return null;
    }

    /// <summary>
    /// Development-only: wipe and reseed the database with a demo agent.
    /// </summary>
    /// <remarks>
    /// This endpoint is intended only for local development and testing: it clears the main demo tables and creates
    /// a seeded agent with a large bond balance to ease testing of UI flows.
    /// </remarks>
    // NOTE (Production Architecture): Remove or protect this endpoint (authentication + environment guard) before any production deployment.
    [HttpGet("reset-db")]
    public async Task<IActionResult> ResetDatabase()
    {
        // Clear demo tables
        _db.TradingTransactions.RemoveRange(_db.TradingTransactions);
        _db.AgentBondSnapshots.RemoveRange(_db.AgentBondSnapshots);
        _db.Agents.RemoveRange(_db.Agents);
        await _db.SaveChangesAsync().ConfigureAwait(false);

        // Recreate a seeded dev agent with 1000 USDC
        var testId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var testWallet = "0x1fC12a9D24eee1147082c2d85BdB56BaFD904121";
        _db.Agents.Add(new Agent(testId, "Dev Agent", testWallet, 1000m, AgentStatus.Active));
        await _db.SaveChangesAsync().ConfigureAwait(false);

        return Ok("Database successfully wiped and seeded with fresh 1000 USDC Agent.");
    }
}
