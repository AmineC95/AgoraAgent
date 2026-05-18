using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using AgoraAgentBackend.Data;
using Microsoft.EntityFrameworkCore;
using AgoraAgentBackend.Domain.Enums;
using AgoraAgentBackend.Services.Blockchain;
using Microsoft.AspNetCore.SignalR;
using AgoraAgentBackend.Services.Infrastructure;
using AgoraAgentBackend.Domain.DTOs;

namespace AgoraAgentBackend.Services.Workers;

public class TransactionMonitoringWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TransactionMonitoringWorker> _logger;
    private readonly TimeSpan _scanInterval = TimeSpan.FromSeconds(15);

    public TransactionMonitoringWorker(IServiceScopeFactory scopeFactory, ILogger<TransactionMonitoringWorker> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TransactionMonitoringWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var arc = scope.ServiceProvider.GetRequiredService<IArcTradingService>();

                var pendings = await db.TradingTransactions
                    .Where(t => t.Status == TradingStatus.Pending)
                    .ToListAsync(stoppingToken).ConfigureAwait(false);

                var hub = scope.ServiceProvider.GetRequiredService<IHubContext<TradeHub>>();

                foreach (var tx in pendings)
                {
                    try
                    {
                        var receipt = await arc.GetTransactionReceiptAsync(tx.TxHash, stoppingToken).ConfigureAwait(false);
                        if (receipt == null) continue;

                        if (receipt.Status != null && receipt.Status.Value == 1)
                        {
                            tx.MarkSuccess();
                            db.TradingTransactions.Update(tx);
                            await db.SaveChangesAsync(stoppingToken).ConfigureAwait(false);
                            _logger.LogInformation("Monitored tx {TxHash} succeeded on-chain", tx.TxHash);
                            // Broadcast update
                            try { await hub.Clients.All.SendAsync("TradeUpdated", tx.ToDto(), cancellationToken: stoppingToken).ConfigureAwait(false); } catch { }
                            continue;
                        }

                        var reason = await arc.GetTransactionRevertReasonAsync(tx.TxHash, stoppingToken).ConfigureAwait(false) ?? "Reverted";
                        tx.MarkFailed(reason);
                        db.TradingTransactions.Update(tx);
                        await db.SaveChangesAsync(stoppingToken).ConfigureAwait(false);
                        _logger.LogWarning("Monitored tx {TxHash} failed on-chain: {Reason}", tx.TxHash, reason);
                        // Broadcast update
                        try { await hub.Clients.All.SendAsync("TradeUpdated", tx.ToDto(), cancellationToken: stoppingToken).ConfigureAwait(false); } catch { }
                    }
                    catch (Exception exTx)
                    {
                        _logger.LogError(exTx, "Error while checking tx {TxHash}", tx.TxHash);
                    }
                }

                // After checking pending transactions, evaluate trading strategy for agents
                try
                {
                    var strategy = scope.ServiceProvider.GetRequiredService<AgoraAgentBackend.Services.Trading.ITradingStrategyService>();
                    var agents = await db.Agents.ToListAsync(stoppingToken).ConfigureAwait(false);
                    foreach (var agent in agents)
                    {
                        try
                        {
                            var should = await strategy.ShouldTradeAsync(agent.Id, stoppingToken).ConfigureAwait(false);
                            if (!should) continue;

                            // Simple action: place a buy for 10% of the agent bond balance
                            var usdcAmount = Math.Round(agent.BondBalance * 0.1m, 6);
                            _logger.LogInformation("Strategy triggered trade for agent {AgentId}, amount {Amount}", agent.Id, usdcAmount);
                            try
                            {
                                await arc.ExecuteTradeAsync(agent.Id, AgoraAgentBackend.Domain.Enums.TradeAction.Buy, usdcAmount, stoppingToken).ConfigureAwait(false);

                                // If trade produced a successful tx, update snapshot
                                var lastTx = await db.TradingTransactions
                                    .Where(t => t.AgentId == agent.Id)
                                    .OrderByDescending(t => t.CreatedAt)
                                    .FirstOrDefaultAsync(stoppingToken).ConfigureAwait(false);

                                if (lastTx != null && lastTx.Status == AgoraAgentBackend.Domain.Enums.TradingStatus.Success)
                                {
                                    await strategy.MarkTradeExecutedAsync(agent.Id, stoppingToken).ConfigureAwait(false);
                                }
                            }
                            catch (Exception exTrade)
                            {
                                _logger.LogError(exTrade, "Error executing strategy trade for agent {AgentId}", agent.Id);
                            }
                        }
                        catch (Exception exAgent)
                        {
                            _logger.LogError(exAgent, "Error while evaluating strategy for agent {AgentId}", agent.Id);
                        }
                    }
                }
                catch (Exception exStrat)
                {
                    _logger.LogError(exStrat, "Error during strategy evaluation loop");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during transaction monitoring loop");
            }

            try
            {
                await Task.Delay(_scanInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException) { }
        }

        _logger.LogInformation("TransactionMonitoringWorker stopping");
    }
}
