using Azure.Storage.Blobs;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace T4L.VideoSearch.Api.Infrastructure;

public class BlobStorageHealthCheck : IHealthCheck
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<BlobStorageHealthCheck> _logger;

    public BlobStorageHealthCheck(BlobServiceClient blobServiceClient, ILogger<BlobStorageHealthCheck> logger)
    {
        _blobServiceClient = blobServiceClient;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to get service properties to verify connectivity
            await _blobServiceClient.GetPropertiesAsync(cancellationToken);
            return HealthCheckResult.Healthy("Blob storage is accessible");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Blob storage health check failed");
            return HealthCheckResult.Unhealthy("Blob storage is not accessible", ex);
        }
    }
}
