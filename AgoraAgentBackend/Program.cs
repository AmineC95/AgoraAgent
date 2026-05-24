using System;
using System.IO;
using System.Linq;
using AgoraAgentBackend.Data;
using AgoraAgentBackend.Services.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AgoraAgentBackend.Domain.Entities;
using AgoraAgentBackend.Domain.Enums;

// Load .env files (if present) into environment variables so Configuration can read them.
// Search current and up to two parent directories for `.env.local` or `.env`.
try
{
    var cwd = Directory.GetCurrentDirectory();
    var candidates = new[]
    {
        Path.Combine(cwd, ".env.local"),
        Path.Combine(cwd, ".env"),
        Path.Combine(cwd, "..", ".env.local"),
        Path.Combine(cwd, "..", ".env"),
        Path.Combine(cwd, "..", "..", ".env.local"),
        Path.Combine(cwd, "..", "..", ".env")
    };

    foreach (var path in candidates)
    {
        if (!File.Exists(path)) continue;
        var lines = File.ReadAllLines(path);
            foreach (var raw in lines)
            {
                var line = raw?.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;
                var idx = line.IndexOf('=');
                if (idx <= 0) continue;
                var key = line.Substring(0, idx).Trim();
                var value = line.Substring(idx + 1).Trim();
                if ((value.StartsWith("\"") && value.EndsWith("\"")) || (value.StartsWith("'") && value.EndsWith("'")))
                {
                    value = value.Substring(1, value.Length - 2);
                }
                try { Environment.SetEnvironmentVariable(key, value); } catch (Exception ex) { Console.WriteLine($"Ignored: {ex.Message}"); }
            }
        break;
    }
    }
    catch (Exception ex) { Console.WriteLine($"Ignored: {ex.Message}"); }

var builder = WebApplication.CreateBuilder(args);

// bind to a stable local URL for predictable OpenAPI paths in dev
builder.WebHost.UseUrls("http://localhost:5000");

// Use native Microsoft OpenAPI generator (replace Swashbuckle)
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();

// SignalR for real-time updates
builder.Services.AddSignalR();

builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    });
    options.AddPolicy("AllowQuasar", policy =>
    {
        policy.WithOrigins("http://localhost:9000").AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    });
});

// Debug: print the connection string read by Configuration and environment variables.
Console.WriteLine($"DEBUG ConnectionString: {builder.Configuration.GetConnectionString("DefaultConnection")}" );
Console.WriteLine($"DEBUG Env ConnectionStrings__DefaultConnection: {Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")}" );
Console.WriteLine($"DEBUG Env DefaultConnection: {Environment.GetEnvironmentVariable("DefaultConnection")}" );
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("DefaultConnection"))
    ));

builder.Services.AddCircleServices();

var rpcUrl = builder.Configuration.GetValue<string>("Arc:RpcUrl");
if (string.IsNullOrWhiteSpace(rpcUrl))
{
    throw new InvalidOperationException("Critical Configuration Missing: 'Arc:RpcUrl' environment variable is not set. Please check your .env or appsettings.json file.");
}
builder.Services.AddBlockchainServices(rpcUrl);

builder.Services.AddScoped<AgoraAgentBackend.Services.Trading.ITradingStrategyService, AgoraAgentBackend.Services.Trading.TradingStrategyService>();

builder.Services.AddHostedService<AgoraAgentBackend.Services.Workers.TransactionMonitoringWorker>();

var app = builder.Build();

// Map OpenAPI (native generator) at runtime — serves JSON at /openapi/v1.json
app.MapOpenApi();

app.UseHttpsRedirection();

// Use the Quasar dev server CORS policy for SignalR negotiation
app.UseCors("AllowQuasar");

app.MapControllers();

// SignalR hub for trade updates
app.MapHub<AgoraAgentBackend.Services.Infrastructure.TradeHub>("/hubs/trade");

// Development-only: ensure a test agent exists so local webhooks can be exercised
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        try
        {
            db.Database.Migrate();
        }
        catch { /* ignore migration failures during quick dev runs */ }

        var testWallet = "0x1fC12a9D24eee1147082c2d85BdB56BaFD904121";
        var testId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var existing = db.Agents.FirstOrDefault(a => a.WalletAddress == testWallet || a.Id == testId);
        if (existing == null)
        {
            db.Agents.Add(new Agent(testId, "Dev Agent", testWallet, 1000m, AgentStatus.Active));
            db.SaveChanges();
        }
        else if (existing.BondBalance <= 0m)
        {
            existing.CreditBond(1000m);
            db.SaveChanges();
        }
    }
    catch
    {
        // ignore seeding errors in dev — backend can still run
    }
}

app.Run();