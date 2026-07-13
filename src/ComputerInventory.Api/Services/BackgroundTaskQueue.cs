using System.Threading.Channels;
using Microsoft.Extensions.Configuration;

namespace ComputerInventory.Api.Services;

/// <summary>
/// Interface for queueing background work items.
/// Use this instead of fire-and-forget Task.Run() for reliable task execution.
/// </summary>
public interface IBackgroundTaskQueue
{
    /// <summary>
    /// Queue a work item for background processing.
    /// </summary>
    /// <param name="workItem">The work item to execute.</param>
    ValueTask QueueBackgroundWorkItemAsync(Func<CancellationToken, Task> workItem);

    /// <summary>
    /// Queue a named work item for background processing with logging context.
    /// </summary>
    /// <param name="workItem">The work item to execute.</param>
    /// <param name="name">A descriptive name for logging.</param>
    ValueTask QueueBackgroundWorkItemAsync(Func<CancellationToken, Task> workItem, string name);

    /// <summary>
    /// Dequeue a work item for processing.
    /// </summary>
    Task<(Func<CancellationToken, Task> WorkItem, string Name)> DequeueAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets the current number of items in the queue.
    /// </summary>
    int QueuedCount { get; }
}

/// <summary>
/// Channel-based background task queue implementation.
/// Provides reliable background task execution with bounded capacity.
/// </summary>
public class BackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly Channel<(Func<CancellationToken, Task> WorkItem, string Name)> _queue;
    private int _queuedCount;

    public BackgroundTaskQueue(int capacity = 100)
    {
        // Bounded channel to prevent memory issues from unbounded queue growth
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        };
        _queue = Channel.CreateBounded<(Func<CancellationToken, Task>, string)>(options);
    }

    public int QueuedCount => _queuedCount;

    public ValueTask QueueBackgroundWorkItemAsync(Func<CancellationToken, Task> workItem)
    {
        return QueueBackgroundWorkItemAsync(workItem, "UnnamedTask");
    }

    public async ValueTask QueueBackgroundWorkItemAsync(Func<CancellationToken, Task> workItem, string name)
    {
        ArgumentNullException.ThrowIfNull(workItem);

        if (!_queue.Writer.TryWrite((workItem, name)))
        {
            // If queue is full, write asynchronously (will wait for space)
            await _queue.Writer.WriteAsync((workItem, name));
        }
        Interlocked.Increment(ref _queuedCount);
    }

    public async Task<(Func<CancellationToken, Task> WorkItem, string Name)> DequeueAsync(CancellationToken cancellationToken)
    {
        var item = await _queue.Reader.ReadAsync(cancellationToken);
        Interlocked.Decrement(ref _queuedCount);
        return item;
    }
}

/// <summary>
/// Background service that processes queued work items.
/// Ensures tasks are executed even during app shutdown (graceful).
/// </summary>
public class BackgroundTaskQueueProcessor : BackgroundService
{
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly ILogger<BackgroundTaskQueueProcessor> _logger;
    private readonly IServiceProvider _serviceProvider;

    public BackgroundTaskQueueProcessor(
        IBackgroundTaskQueue taskQueue,
        ILogger<BackgroundTaskQueueProcessor> logger,
        IServiceProvider serviceProvider)
    {
        _taskQueue = taskQueue;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background Task Queue Processor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var (workItem, name) = await _taskQueue.DequeueAsync(stoppingToken);

                try
                {
                    _logger.LogDebug("Processing background task: {TaskName}", name);
                    await workItem(stoppingToken);
                    _logger.LogDebug("Completed background task: {TaskName}", name);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Background task {TaskName} cancelled due to shutdown", name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing background task: {TaskName}", name);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error dequeuing background task");
                await Task.Delay(1000, stoppingToken); // Prevent tight loop on persistent errors
            }
        }

        // Process remaining items during graceful shutdown (with timeout)
        var remainingCount = _taskQueue.QueuedCount;
        if (remainingCount > 0)
        {
            _logger.LogInformation("Processing {Count} remaining background tasks during shutdown", remainingCount);
            
            using var shutdownCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            
            while (_taskQueue.QueuedCount > 0 && !shutdownCts.IsCancellationRequested)
            {
                try
                {
                    var (workItem, name) = await _taskQueue.DequeueAsync(shutdownCts.Token);
                    _logger.LogDebug("Processing remaining task during shutdown: {TaskName}", name);
                    await workItem(shutdownCts.Token);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error processing task during shutdown");
                }
            }
        }

        _logger.LogInformation("Background Task Queue Processor stopped");
    }
}

/// <summary>
/// Extension methods for registering the background task queue.
/// </summary>
public static class BackgroundTaskQueueExtensions
{
    public static IServiceCollection AddBackgroundTaskQueue(this IServiceCollection services, IConfiguration configuration)
    {
        var capacity = configuration.GetValue("BackgroundTaskQueue:Capacity", 100);
        services.AddSingleton<IBackgroundTaskQueue>(new BackgroundTaskQueue(capacity));
        services.AddHostedService<BackgroundTaskQueueProcessor>();
        return services;
    }
}
