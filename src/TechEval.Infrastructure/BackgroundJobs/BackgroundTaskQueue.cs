using System.Threading.Channels;
using TechEval.Application.Services;

namespace TechEval.Infrastructure.BackgroundJobs;

public class BackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly Channel<int> _channel = Channel.CreateUnbounded<int>();

    public void EnqueueJob(int jobId)
        => _channel.Writer.TryWrite(jobId);

    public IAsyncEnumerable<int> DequeueAllAsync(CancellationToken ct)
        => _channel.Reader.ReadAllAsync(ct);
}
