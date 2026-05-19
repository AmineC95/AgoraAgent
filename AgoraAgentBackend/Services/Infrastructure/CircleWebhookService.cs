using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AgoraAgentBackend.Data;
using AgoraAgentBackend.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace AgoraAgentBackend.Services.Infrastructure;

public class CircleWebhookService : ICircleWebhookService
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly IHubContext<TradeHub>? _hubContext;
    private readonly ILogger<CircleWebhookService> _logger;
    private readonly IHostEnvironment? _env;

    public CircleWebhookService(ApplicationDbContext db, IHttpClientFactory httpClientFactory, IHubContext<TradeHub> hubContext, ILogger<CircleWebhookService> logger, IHostEnvironment env)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _hubContext = hubContext;
        _logger = logger;
        _env = env;
    }

    // Protected constructor used for testing where an HttpClientFactory is not needed
    protected CircleWebhookService(ApplicationDbContext db, ILogger<CircleWebhookService> logger)
    {
        _db = db;
        _httpClientFactory = null;
        _logger = logger;
        _env = null;
    }

    public async Task<bool> HandleAsync(HttpRequest request, CancellationToken cancellationToken = default)
    {
        string body;
        try
        {
            request.EnableBuffering();
            using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
            body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            request.Body.Position = 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read request body");
            throw;
        }

        try
        {
            if (_env?.IsDevelopment() == true)
            {
                _logger.LogInformation("Development environment: skipping SNS signature verification");
            }
            else
            {
                if (!await VerifySignatureAsync(request, body, cancellationToken).ConfigureAwait(false))
                {
                    _logger.LogWarning("Invalid signature on Circle webhook");
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Signature verification error");
            throw;
        }

        // Idempotence: check x-amz-sns-message-id after signature verified
        string? messageId = null;
        if (request.Headers.TryGetValue("x-amz-sns-message-id", out var mid))
        {
            messageId = mid.ToString();
            if (!string.IsNullOrEmpty(messageId))
            {
                var already = await _db.ProcessedWebhooks.AnyAsync(p => p.MessageId == messageId, cancellationToken).ConfigureAwait(false);
                if (already)
                {
                    _logger.LogInformation("Duplicate SNS message {MessageId} received - ignoring.", messageId);
                    return true;
                }
            }
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // Ignore non-finalized mint events
            if (!body.Contains("gateway.mint.finalized", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Webhook is not gateway.mint.finalized; ignoring.");
                return true;
            }

            // Extract amount and agent identity (try several paths)
            decimal amount = 0m;
            string? wallet = null;
            Guid? agentId = null;

            if (TryGetString(root, "walletAddress", out var w)) wallet = w;
            if (TryGetString(root, "agentWallet", out var aw)) wallet ??= aw;
            if (TryGetString(root, "agentId", out var aid) && Guid.TryParse(aid, out var g)) agentId ??= g;

            if (root.TryGetProperty("data", out var data))
            {
                _ = TryGetString(data, "walletAddress", out var dw);
                wallet ??= dw;
                _ = TryGetString(data, "agentId", out var da);
                if (Guid.TryParse(da, out var g2)) agentId ??= g2;
                if (TryGetString(data, "amount", out var damt) && decimal.TryParse(damt, out var damtDec)) amount = damtDec;
                if (data.TryGetProperty("amount", out var amtProp) && amtProp.ValueKind == JsonValueKind.Number && amtProp.TryGetDecimal(out var amtDec)) amount = amtDec;
            }

            if (root.TryGetProperty("payload", out var payload))
            {
                _ = TryGetString(payload, "walletAddress", out var pw);
                wallet ??= pw;
                _ = TryGetString(payload, "agentId", out var pa);
                if (Guid.TryParse(pa, out var g3)) agentId ??= g3;
                if (TryGetString(payload, "amount", out var pamt) && decimal.TryParse(pamt, out var pamtD)) amount = pamtD;
                if (payload.TryGetProperty("amount", out var pamtProp) && pamtProp.ValueKind == JsonValueKind.Number && pamtProp.TryGetDecimal(out var pamtDec)) amount = pamtDec;
            }

            if (amount == 0m && TryGetString(root, "amount", out var amtRoot) && decimal.TryParse(amtRoot, out var amtParsed)) amount = amtParsed;

            if (amount <= 0m)
            {
                _logger.LogWarning("Deposit amount missing or zero in Circle webhook payload");
                return true;
            }

            // Resolve agent by id or wallet
            Agent? agent = null;
            if (agentId.HasValue)
            {
                agent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == agentId.Value, cancellationToken).ConfigureAwait(false);
            }
            if (agent == null && !string.IsNullOrEmpty(wallet))
            {
                agent = await _db.Agents.FirstOrDefaultAsync(a => a.WalletAddress == wallet, cancellationToken).ConfigureAwait(false);
            }

            if (agent == null)
            {
                _logger.LogWarning("Agent not found for wallet {Wallet} or id {Id}", wallet, agentId);
                return true;
            }

            agent.CreditBond(amount);
            _db.Agents.Update(agent);

            if (!string.IsNullOrEmpty(messageId))
            {
                _db.ProcessedWebhooks.Add(new ProcessedWebhook(Guid.Empty, messageId, DateTime.UtcNow));
            }

            try
            {
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogWarning(dbEx, "DbUpdateException while saving processed webhook or agent; possible duplicate message {MessageId}", messageId);
            }

            _logger.LogInformation("Agent {AgentId} credited {Amount} USDC from Circle gateway.mint.finalized", agent.Id, amount);

            // Emit SignalR event to notify frontend of updated balance (if hub context is available)
            try
            {
                if (_hubContext != null)
                {
                    var signalrPayload = new { AgentId = agent.Id, BondBalance = agent.BondBalance };
                    _logger.LogInformation("Emitting BalanceUpdated SignalR event for Agent {AgentId} with balance {BondBalance}", agent.Id, agent.BondBalance);
                    await _hubContext.Clients.All.SendAsync("BalanceUpdated", signalrPayload, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to emit BalanceUpdated SignalR event for Agent {AgentId}", agent.Id);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Circle webhook");
            throw;
        }
    }

    private bool TryGetString(JsonElement element, string name, out string? value)
    {
        value = null;
        if (element.ValueKind != JsonValueKind.Object) return false;
        if (!element.TryGetProperty(name, out var prop)) return false;
        if (prop.ValueKind == JsonValueKind.String)
        {
            value = prop.GetString();
            return true;
        }

        value = prop.ToString();
        return true;
    }

    protected virtual async Task<bool> VerifySignatureAsync(HttpRequest request, string body, CancellationToken cancellationToken)
    {
        try
        {
            // Signature may be in header or in JSON body
            string? signatureBase64 = null;
            if (request.Headers.TryGetValue("x-amz-sns-signature", out var headerSig)) signatureBase64 = headerSig.ToString();

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            string? signingCertUrl = null;
            if (TryGetString(root, "SigningCertURL", out var s1)) signingCertUrl = s1;
            if (string.IsNullOrEmpty(signingCertUrl) && TryGetString(root, "signingCertUrl", out var s2)) signingCertUrl = s2;
            if (string.IsNullOrEmpty(signatureBase64) && TryGetString(root, "Signature", out var s3)) signatureBase64 = s3;

            if (string.IsNullOrEmpty(signatureBase64) || string.IsNullOrEmpty(signingCertUrl))
            {
                _logger.LogWarning("Missing signature or signing cert url in payload");
                return false;
            }

            if (!Uri.TryCreate(signingCertUrl, UriKind.Absolute, out var certUri))
            {
                _logger.LogWarning("Invalid SigningCertURL: {Url}", signingCertUrl);
                return false;
            }

            if (!certUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) || !certUri.Host.Contains("amazonaws.com"))
            {
                _logger.LogWarning("SigningCertURL host invalid or not https: {Url}", signingCertUrl);
                return false;
            }

            if (_httpClientFactory == null)
            {
                _logger.LogWarning("HttpClientFactory not configured; cannot verify SNS certificate");
                return false;
            }

            var client = _httpClientFactory.CreateClient("sns-cert");
            using var certStream = await client.GetStreamAsync(certUri, cancellationToken).ConfigureAwait(false);
            using var reader = new StreamReader(certStream, Encoding.UTF8);
            var pem = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            // Prefer CreateFromPem where available to avoid deprecated constructors/imports
            X509Certificate2 cert;
            try
            {
                cert = X509Certificate2.CreateFromPem(pem);
            }
            catch (Exception exPem)
            {
                _logger.LogWarning(exPem, "Unable to load certificate from PEM payload");
                return false;
            }

            using var rsa = cert.GetRSAPublicKey();
            if (rsa == null)
            {
                _logger.LogWarning("No RSA public key in certificate");
                return false;
            }

            var signatureBytes = Convert.FromBase64String(signatureBase64);
            var dataBytes = Encoding.UTF8.GetBytes(body);

            // Try SHA256 then fallback to SHA1 for older signatures
            bool verified = rsa.VerifyData(dataBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            if (!verified)
            {
                verified = rsa.VerifyData(dataBytes, signatureBytes, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);
            }

            if (!verified)
            {
                _logger.LogWarning("SNS signature verification failed for cert {Cert}", signingCertUrl);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while verifying SNS signature");
            throw;
        }
    }
}
