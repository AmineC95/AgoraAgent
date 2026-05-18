using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using AgoraAgentBackend.Data;
using AgoraAgentBackend.Domain.Entities;
using AgoraAgentBackend.Domain.Enums;
using AgoraAgentBackend.Services.Infrastructure;
using AgoraAgentBackend.Services.Blockchain;
using AgoraAgentBackend.Services.Trading;
using Microsoft.AspNetCore.Http;
using Moq;
using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace AgoraAgentBackend.Tests.E2E;

public class TradeFlowTests
{
    private class TestCircleWebhookService : CircleWebhookService
    {
        public TestCircleWebhookService(ApplicationDbContext db, ILogger<CircleWebhookService> logger)
            : base(db, logger)
        {
        }

        protected override Task<bool> VerifySignatureAsync(HttpRequest request, string body, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }
    }

    private class FakeArcTradingService : IArcTradingService
    {
        private readonly ApplicationDbContext _db;

        public FakeArcTradingService(ApplicationDbContext db)
        {
            _db = db;
        }

        public Task<string?> GetTransactionRevertReasonAsync(string txHash, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(null);
        }

        public Task<TransactionReceipt?> GetTransactionReceiptAsync(string txHash, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<TransactionReceipt?>(null);
        }

        public Task<(int SuccessCount, int FailedCount, decimal TotalBond)> GetTradingStatsAsync(Guid? agentId = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((0,0,0m));
        }

        public Task<(decimal NativeBalance, decimal UsdcBalance)> GetAgentOnChainBalancesAsync(Guid agentId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((0m,0m));
        }

        public async Task ExecuteTradeAsync(Guid agentId, TradeAction action, decimal usdcAmount, CancellationToken cancellationToken = default)
        {
            var tx = new TradingTransaction(Guid.Empty, agentId, "0xdeadbeef", action, usdcAmount, 0m);
            tx.MarkSuccess();
            _db.TradingTransactions.Add(tx);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    [Fact]
    public async Task Webhook_To_Strategy_Triggers_Trade_Worker_Run()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<ApplicationDbContext>(opts => opts.UseInMemoryDatabase(dbName));
        services.AddScoped<ITradingStrategyService, TradingStrategyService>();
        services.AddScoped<IArcTradingService, FakeArcTradingService>();

        var provider = services.BuildServiceProvider();

        // Seed agent and initial snapshot
        var agentId = Guid.NewGuid();
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var agent = new Agent(agentId, "Agent E2E", "0xwallet-e2e", 100m, AgentStatus.Active);
            db.Agents.Add(agent);
            db.AgentBondSnapshots.Add(new AgentBondSnapshot(Guid.Empty, agentId, 100m, DateTime.UtcNow));
            await db.SaveChangesAsync();
        }

        // Process webhook that credits +20 (20% increase)
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<CircleWebhookService>>();
            var svc = new TestCircleWebhookService(db, logger);

            var payload = $"{{\"type\":\"gateway.mint.finalized\",\"agentId\":\"{agentId}\",\"amount\":\"20\"}}";
            var context = new DefaultHttpContext();
            context.Request.Headers["x-amz-sns-message-id"] = "msg-e2e-1";
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));

            var res = await svc.HandleAsync(context.Request);
            Assert.True(res);
        }

        // Run worker for a short period to allow strategy evaluation and fake trade
        using (var scope = provider.CreateScope())
        {
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
            var logger = new Moq.Mock<ILogger<AgoraAgentBackend.Services.Workers.TransactionMonitoringWorker>>().Object;
            var worker = new AgoraAgentBackend.Services.Workers.TransactionMonitoringWorker(scopeFactory, logger);

            var cts = new CancellationTokenSource();
            var runTask = worker.StartAsync(cts.Token);

            // Wait for a brief moment to let the worker run an iteration
            await Task.Delay(2000);

            await worker.StopAsync(CancellationToken.None);
        }

        // Assert that a trading transaction was created and marked success
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tx = await db.TradingTransactions.FirstOrDefaultAsync(t => t.AgentId == agentId);
            Assert.NotNull(tx);
            Assert.Equal(TradingStatus.Success, tx.Status);
        }
    }
}
