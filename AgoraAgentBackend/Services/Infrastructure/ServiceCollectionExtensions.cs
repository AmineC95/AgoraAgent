using System;
using Microsoft.Extensions.DependencyInjection;
using AgoraAgentBackend.Services.Blockchain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using AgoraAgentBackend.Data;

namespace AgoraAgentBackend.Services.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCircleServices(this IServiceCollection services)
    {
        services.AddScoped<ICircleWebhookService, CircleWebhookService>();
        services.AddHttpClient("sns-cert", client => client.Timeout = TimeSpan.FromSeconds(10));
        return services;
    }

    public static IServiceCollection AddBlockchainServices(this IServiceCollection services, string rpcUrl)
    {
        if (string.IsNullOrEmpty(rpcUrl)) throw new ArgumentNullException(nameof(rpcUrl));
        services.AddScoped<IArcTradingService>(sp =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            var logger = sp.GetRequiredService<ILogger<AgoraAgentBackend.Services.Blockchain.ArcTradingService>>();
            var configuration = sp.GetRequiredService<IConfiguration>();
            var privateKey = configuration.GetValue<string>("Arc:AgentPrivateKey");
            if (string.IsNullOrEmpty(privateKey)) throw new InvalidOperationException("Arc:AgentPrivateKey is not configured.");
            return new ArcTradingService(db, rpcUrl, privateKey, logger);
        });
        return services;
    }
}
