using System.Threading.Channels;

namespace T4L.VideoSearch.Api.Infrastructure.BackgroundJobs;

/// <summary>
/// In-memory channel-based job queue for background processing
/// </summary>
public class ChannelJobQueue : IJobQueue
{
    private readonly Channel<JobEnvelope> _channel;
    private readonly ILogger<ChannelJobQueue> _logger;

    public ChannelJobQueue(ILogger<ChannelJobQueue> logger)
    {
        _logger = logger;
        // Bounded channel to prevent memory issues
        _channel = Channel.CreateBounded<JobEnvelope>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });
    }

    public async ValueTask EnqueueAsync<TJob>(TJob job, CancellationToken cancellationToken = default)
        where TJob : class, IJob
    {
        var envelope = new JobEnvelope(typeof(TJob), job, job.JobId);

        await _channel.Writer.WriteAsync(envelope, cancellationToken);

        _logger.LogInformation(
            "Enqueued job {JobType} with ID {JobId}",
            typeof(TJob).Name,
            job.JobId);
    }

    /// <summary>
    /// Read jobs from the queue (for background processor)
    /// </summary>
    public ChannelReader<JobEnvelope> Reader => _channel.Reader;
}

/// <summary>
/// Wrapper for jobs in the queue
/// </summary>
public record JobEnvelope(Type JobType, object Job, Guid JobId);
