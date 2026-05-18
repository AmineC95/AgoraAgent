using Microsoft.AspNetCore.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AgoraAgentBackend.Services.Infrastructure;

public interface ICircleWebhookService
{
    /// <summary>
    /// Handles a Circle gateway webhook (SNS-like) request.
    /// Returns true when signature is valid and processing completed (or intentionally ignored).
    /// Returns false when signature validation fails.
    /// </summary>
    Task<bool> HandleAsync(HttpRequest request, CancellationToken cancellationToken = default);
}
