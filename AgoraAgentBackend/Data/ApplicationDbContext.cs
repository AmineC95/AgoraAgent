using Microsoft.EntityFrameworkCore;
using AgoraAgentBackend.Domain.Entities;

namespace AgoraAgentBackend.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Agent> Agents { get; set; } = null!;
    public DbSet<TradingTransaction> TradingTransactions { get; set; } = null!;
    public DbSet<UserFollower> UserFollowers { get; set; } = null!;
    public DbSet<ProcessedWebhook> ProcessedWebhooks { get; set; } = null!;
    public DbSet<AgentBondSnapshot> AgentBondSnapshots { get; set; } = null!;
}
