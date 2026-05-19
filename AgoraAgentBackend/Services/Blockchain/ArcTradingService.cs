using System;
using System.Threading;
using System.Threading.Tasks;
using AgoraAgentBackend.Data;
using AgoraAgentBackend.Domain.Entities;
using AgoraAgentBackend.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Contracts;
using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;
using System.Globalization;
using Nethereum.RPC.Eth.DTOs;
using AgoraAgentBackend.Domain.Exceptions;


namespace AgoraAgentBackend.Services.Blockchain;

public class ArcTradingService : IArcTradingService
{
    private readonly ApplicationDbContext _db;
    private readonly string _rpcUrl;
    private readonly string _privateKey;
    private readonly ILogger<ArcTradingService> _logger;

    // USDC contract address placeholder on Arc testnet
    private const string UsdcContractAddress = "0x0000000000000000000000000000000000001234";
    private const int UsdcDecimals = 6;
    private const int NativeDecimals = 18;

    public ArcTradingService(ApplicationDbContext db, string rpcUrl, string privateKey, ILogger<ArcTradingService> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _rpcUrl = rpcUrl ?? throw new ArgumentNullException(nameof(rpcUrl));
        _privateKey = privateKey ?? throw new ArgumentNullException(nameof(privateKey));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteTradeAsync(Guid agentId, TradeAction action, decimal usdcAmount, CancellationToken cancellationToken = default)
    {
        if (usdcAmount <= 0) throw new ArgumentOutOfRangeException(nameof(usdcAmount));

        var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == agentId, cancellationToken).ConfigureAwait(false);
        if (agent == null) throw new InvalidOperationException("Agent not found");

        if (agent.BondBalance < usdcAmount) throw new InvalidOperationException("Insufficient bond balance for trade");

        _logger.LogDebug("Preparing on-chain transfer for agent {AgentId} amount {Amount}", agentId, usdcAmount);

        // Prepare account and web3 (using the configured signer)
        var account = new Account(_privateKey, 5042002);
        var web3 = new Web3(account, _rpcUrl);
        web3.TransactionManager.UseLegacyAsDefault = true;

        // Use native token transfer to prove the signer and log the on-chain transaction.
        TransactionReceipt? receipt = null;
        try
        {
            // TransferEtherAndWaitForReceiptAsync expects amount in Ether (decimal)
            receipt = await web3.Eth.GetEtherTransferService().TransferEtherAndWaitForReceiptAsync(account.Address, usdcAmount).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending native transfer for agent {AgentId}", agentId);
            var failedTx = new TradingTransaction(Guid.Empty, agentId, string.Empty, action, usdcAmount, 0m);
            failedTx.MarkFailed(ex.Message ?? "Exception during native transfer");
            _db.TradingTransactions.Add(failedTx);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        var txHash = receipt?.TransactionHash ?? string.Empty;
        var tx = new TradingTransaction(Guid.Empty, agentId, txHash, action, usdcAmount, 0m);
        if (receipt != null && receipt.Status != null && receipt.Status.Value == 1)
        {
            tx.MarkSuccess();
            _db.TradingTransactions.Add(tx);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Transaction {TxHash} succeeded on-chain for agent {AgentId}", txHash, agentId);
            return;
        }

        tx.MarkFailed("Transaction failed or no receipt status");
        _db.TradingTransactions.Add(tx);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogWarning("Transaction {TxHash} failed on-chain for agent {AgentId}", txHash, agentId);
    }

    public async Task<(decimal NativeBalance, decimal UsdcBalance)> GetAgentOnChainBalancesAsync(Guid agentId, CancellationToken cancellationToken = default)
    {
        var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == agentId, cancellationToken).ConfigureAwait(false);
        if (agent == null) throw new InvalidOperationException("Agent not found");

        var web3 = new Web3(_rpcUrl);

        var balanceOfHandler = web3.Eth.GetContractQueryHandler<BalanceOfFunction>();
        var usdcBig = await balanceOfHandler.QueryAsync<BigInteger>(UsdcContractAddress, new BalanceOfFunction { Owner = agent.WalletAddress }).ConfigureAwait(false);
        var usdc = ToDecimalFromTokenAmount(usdcBig, UsdcDecimals);

        var nativeWei = await web3.Eth.GetBalance.SendRequestAsync(agent.WalletAddress).ConfigureAwait(false);
        var native = ToDecimalFromTokenAmount(nativeWei.Value, NativeDecimals);

        return (native, usdc);
    }

    public async Task<(int SuccessCount, int FailedCount, decimal TotalBond)> GetTradingStatsAsync(Guid? agentId = null, CancellationToken cancellationToken = default)
    {
        var successCount = await _db.TradingTransactions.CountAsync(t => t.Status == TradingStatus.Success && (agentId == null || t.AgentId == agentId), cancellationToken).ConfigureAwait(false);
        var failedCount = await _db.TradingTransactions.CountAsync(t => t.Status == TradingStatus.Failed && (agentId == null || t.AgentId == agentId), cancellationToken).ConfigureAwait(false);
        decimal totalBond;
        if (agentId.HasValue)
        {
            var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == agentId.Value, cancellationToken).ConfigureAwait(false);
            totalBond = agent?.BondBalance ?? 0m;
        }
        else
        {
            totalBond = await _db.Agents.SumAsync(a => a.BondBalance, cancellationToken).ConfigureAwait(false);
        }

        return (successCount, failedCount, totalBond);
    }

    public async Task<TransactionReceipt?> GetTransactionReceiptAsync(string txHash, CancellationToken cancellationToken = default)
    {
        var web3 = new Web3(_rpcUrl);
        return await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash).ConfigureAwait(false);
    }

    public async Task<string?> GetTransactionRevertReasonAsync(string txHash, CancellationToken cancellationToken = default)
    {
        var web3 = new Web3(_rpcUrl);
        var txInfo = await web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(txHash).ConfigureAwait(false);
        if (txInfo == null || string.IsNullOrEmpty(txInfo.Input)) return null;

        try
        {
            var callResult = await web3.Eth.Transactions.Call.SendRequestAsync(new CallInput(txInfo.Input, txInfo.To)).ConfigureAwait(false);
            // If eth_call succeeds but receipt failed, we still don't have a reason; return generic
            return null;
        }
        catch (Exception ex)
        {
            var msg = ex.Message ?? string.Empty;
            var idx = msg.IndexOf("revert", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                return msg.Substring(idx).Trim();
            }

            return msg;
        }
    }

    private static BigInteger ToTokenAmount(decimal amount, int decimals)
    {
        if (decimals < 0) throw new ArgumentOutOfRangeException(nameof(decimals));
        decimal scale = 1m;
        for (int i = 0; i < decimals; i++) scale *= 10m;
        var scaled = decimal.Truncate(amount * scale);
        var s = scaled.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return BigInteger.Parse(s);
    }

    private static decimal ToDecimalFromTokenAmount(BigInteger amount, int decimals)
    {
        var s = amount.ToString();
        var d = decimal.Parse(s, CultureInfo.InvariantCulture);
        decimal scale = 1m;
        for (int i = 0; i < decimals; i++) scale *= 10m;
        return decimal.Divide(d, scale);
    }

    private static async Task<TransactionReceipt?> WaitForReceiptAsync(Web3 web3, string txHash, TimeSpan timeout)
    {
        var start = DateTime.UtcNow;
        while (DateTime.UtcNow - start < timeout)
        {
            var receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash).ConfigureAwait(false);
            if (receipt != null) return receipt;
            await Task.Delay(1500).ConfigureAwait(false);
        }
        return null;
    }

    [Function("transfer", "bool")]
    public class TransferFunction : Nethereum.Contracts.FunctionMessage
    {
        [Parameter("address", "_to", 1)]
        public string To { get; set; } = string.Empty;

        [Parameter("uint256", "_value", 2)]
        public BigInteger TokenAmount { get; set; }
    }

    [Function("balanceOf", "uint256")]
    public class BalanceOfFunction : Nethereum.Contracts.FunctionMessage
    {
        [Parameter("address", "owner", 1)]
        public string Owner { get; set; } = string.Empty;
    }
}
