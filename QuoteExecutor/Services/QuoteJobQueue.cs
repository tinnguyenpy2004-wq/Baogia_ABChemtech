using System.Collections.Concurrent;
using System.Threading.Channels;
using QuoteExecutor.Models;

namespace QuoteExecutor.Services;

public interface IQuoteJobQueue
{
    ValueTask EnqueueAsync(QuoteJob job, CancellationToken cancellationToken = default);
    IAsyncEnumerable<QuoteJob> ReadAllAsync(CancellationToken cancellationToken = default);
    QuoteJob? GetJob(string id);
    void UpdateJob(QuoteJob job);
}

public class QuoteJobQueue : IQuoteJobQueue
{
    private readonly Channel<QuoteJob> _channel;
    private readonly ConcurrentDictionary<string, QuoteJob> _jobs = new();

    public QuoteJobQueue()
    {
        // UnboundedChannel đảm bảo không bị nghẽn khi nhận nhiều request đồng thời
        var options = new UnboundedChannelOptions
        {
            SingleReader = true // Chỉ có 1 BackgroundService đọc tuần tự từng job
        };
        _channel = Channel.CreateUnbounded<QuoteJob>(options);
    }

    public async ValueTask EnqueueAsync(QuoteJob job, CancellationToken cancellationToken = default)
    {
        _jobs[job.Id] = job;
        await _channel.Writer.WriteAsync(job, cancellationToken);
    }

    public IAsyncEnumerable<QuoteJob> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }

    public QuoteJob? GetJob(string id)
    {
        _jobs.TryGetValue(id, out var job);
        return job;
    }

    public void UpdateJob(QuoteJob job)
    {
        _jobs[job.Id] = job;
    }
}
