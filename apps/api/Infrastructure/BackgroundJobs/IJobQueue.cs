namespace T4L.VideoSearch.Api.Infrastructure.BackgroundJobs;

/// <summary>
/// Interface for enqueueing background jobs
/// </summary>
public interface IJobQueue
{
    /// <summary>
    /// Enqueue a job for background processing
    /// </summary>
    ValueTask EnqueueAsync<TJob>(TJob job, CancellationToken cancellationToken = default) where TJob : class, IJob;
}

/// <summary>
/// Marker interface for background jobs
/// </summary>
public interface IJob
{
    /// <summary>
    /// Unique identifier for this job instance
    /// </summary>
    Guid JobId { get; }
}

/// <summary>
/// Interface for handling a specific job type
/// </summary>
/// <typeparam name="TJob">The job type to handle</typeparam>
public interface IJobHandler<in TJob> where TJob : class, IJob
{
    /// <summary>
    /// Process the job
    /// </summary>
    Task HandleAsync(TJob job, CancellationToken cancellationToken);
}
