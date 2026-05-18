using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AgoraAgentBackend.Data;
using AgoraAgentBackend.Domain.Entities;
using AgoraAgentBackend.Domain.Enums;
using AgoraAgentBackend.Services.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AgoraAgentBackend.Tests;

public class CircleWebhookServiceTests
{
    private class TestCircleWebhookService : CircleWebhookService
    {
        private readonly bool _verifyResult;

        public TestCircleWebhookService(ApplicationDbContext db, ILogger<CircleWebhookService> logger, bool verifyResult)
            : base(db, logger)
        {
            _verifyResult = verifyResult;
        }

        protected override Task<bool> VerifySignatureAsync(HttpRequest request, string body, CancellationToken cancellationToken)
        {
            return Task.FromResult(_verifyResult);
        }
    }

    [Fact]
    public async Task HandleAsync_InvalidSignature_ReturnsFalse()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        var loggerMock = new Mock<ILogger<CircleWebhookService>>();

        var service = new TestCircleWebhookService(db, loggerMock.Object, verifyResult: false);

        var context = new DefaultHttpContext();
        var json = "{}";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var result = await service.HandleAsync(context.Request);
        Assert.False(result);
    }

    [Fact]
    public async Task HandleAsync_DuplicateMessageId_OnlyCreditsOnce()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);

        var agentId = Guid.NewGuid();
        var agent = new Agent(agentId, "Agent A", "0xwallet", 100m, AgentStatus.Active);
        db.Agents.Add(agent);
        await db.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<CircleWebhookService>>();
        var service = new TestCircleWebhookService(db, loggerMock.Object, verifyResult: true);

        var messageId = "msg-123";
        var payload = $"{{\"type\":\"gateway.mint.finalized\",\"agentId\":\"{agentId}\",\"amount\":\"50\"}}";

        var context1 = new DefaultHttpContext();
        context1.Request.Headers["x-amz-sns-message-id"] = messageId;
        context1.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        var res1 = await service.HandleAsync(context1.Request);
        Assert.True(res1);

        var dbAgent = await db.Agents.FindAsync(agentId);
        Assert.NotNull(dbAgent);
        Assert.Equal(150m, dbAgent.BondBalance);

        var context2 = new DefaultHttpContext();
        context2.Request.Headers["x-amz-sns-message-id"] = messageId;
        context2.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        var res2 = await service.HandleAsync(context2.Request);
        Assert.True(res2);

        dbAgent = await db.Agents.FindAsync(agentId);
        Assert.NotNull(dbAgent);
        Assert.Equal(150m, dbAgent.BondBalance);
    }
}
