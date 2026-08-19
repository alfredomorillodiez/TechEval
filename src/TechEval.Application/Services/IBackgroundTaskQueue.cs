namespace TechEval.Application.Services;

public interface IBackgroundTaskQueue
{
    void EnqueueJob(int jobId);
    IAsyncEnumerable<int> DequeueAllAsync(CancellationToken ct);
}
