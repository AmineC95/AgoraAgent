using AgoraAgentBackend.Data;
using AgoraAgentBackend.Services.Infrastructure;
using Microsoft.EntityFrameworkCore;

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

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("DefaultConnection"))
    ));

builder.Services.AddCircleServices();
var rpcUrl = builder.Configuration.GetValue<string>("Arc:RpcUrl") ?? "https://rpc.arc.example";
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

app.Run();