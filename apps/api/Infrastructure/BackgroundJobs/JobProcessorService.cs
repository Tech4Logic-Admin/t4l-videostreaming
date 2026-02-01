namespace T4L.VideoSearch.Api.Infrastructure.BackgroundJobs;

/// <summary>
/// Background service that processes jobs from the queue
/// </summary>
public class JobProcessorService : BackgroundService
{
    private readonly ChannelJobQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<JobProcessorService> _logger;

    public JobProcessorService(
        ChannelJobQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<JobProcessorService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Job processor service started");

        await foreach (var envelope in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessJobAsync(envelope, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unhandled error processing job {JobType} with ID {JobId}",
                    envelope.JobType.Name,
                    envelope.JobId);
            }
        }

        _logger.LogInformation("Job processor service stopped");
    }

    private async Task ProcessJobAsync(JobEnvelope envelope, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing job {JobType} with ID {JobId}",
            envelope.JobType.Name,
            envelope.JobId);

        using var scope = _scopeFactory.CreateScope();

        // Find the handler for this job type
        var handlerType = typeof(IJobHandler<>).MakeGenericType(envelope.JobType);
        var handler = scope.ServiceProvider.GetService(handlerType);

        if (handler == null)
        {
            _logger.LogWarning(
                "No handler registered for job type {JobType}",
                envelope.JobType.Name);
            return;
        }

        // Invoke the handler
        var handleMethod = handlerType.GetMethod("HandleAsync");
        if (handleMethod == null)
        {
            _logger.LogError("HandleAsync method not found on handler {HandlerType}", handlerType.Name);
            return;
        }

        var task = (Task?)handleMethod.Invoke(handler, [envelope.Job, cancellationToken]);
        if (task != null)
        {
            await task;
        }

        _logger.LogInformation(
            "Completed job {JobType} with ID {JobId}",
            envelope.JobType.Name,
            envelope.JobId);
    }
}
