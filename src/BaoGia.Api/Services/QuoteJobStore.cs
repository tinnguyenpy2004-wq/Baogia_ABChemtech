using System.Collections.Concurrent;
using BaoGia.Api.Models;

namespace BaoGia.Api.Services;

public sealed class QuoteJobStore
{
    private readonly ConcurrentDictionary<Guid, QuoteJobRecord> jobs = new();
    private readonly ConcurrentDictionary<string, Guid> idempotencyKeys = new(StringComparer.Ordinal);
    private readonly string outputDirectory = Path.Combine(AppContext.BaseDirectory, "generated");

    public QuoteJobStore()
    {
        Directory.CreateDirectory(outputDirectory);
    }

    public IReadOnlyCollection<QuoteJobRecord> All() => jobs.Values.OrderByDescending(job => job.CreatedAt).ToArray();

    public QuoteJobRecord? Find(Guid id) => jobs.TryGetValue(id, out var job) ? job : null;

    public QuoteJobRecord? FindByIdempotencyKey(string key) =>
        idempotencyKeys.TryGetValue(key, out var id) ? Find(id) : null;

    public QuoteJobRecord Create(CreateQuoteRequest request)
    {
        var existing = FindByIdempotencyKey(request.IdempotencyKey);
        if (existing is not null)
        {
            return existing;
        }

        var now = DateTimeOffset.UtcNow;
        var job = new QuoteJobRecord(
            Guid.NewGuid(),
            $"BG-{now:yyyyMMdd-HHmmss}-{Random.Shared.Next(100, 999)}",
            request,
            QuoteJobStatus.Pending,
            now,
            null,
            null,
            0);

        if (idempotencyKeys.TryAdd(request.IdempotencyKey, job.Id))
        {
            jobs[job.Id] = job;
            return job;
        }

        return FindByIdempotencyKey(request.IdempotencyKey)!;
    }

    public bool TryClaim(out QuoteJobRecord? job)
    {
        job = jobs.Values.FirstOrDefault(candidate => candidate.Status == QuoteJobStatus.Pending);
        if (job is null)
        {
            return false;
        }

        job.Status = QuoteJobStatus.Processing;
        job.Attempt++;
        return true;
    }

    public void Complete(QuoteJobRecord job, string fileName)
    {
        job.OutputFileName = fileName;
        job.Status = QuoteJobStatus.Completed;
    }

    public void Fail(QuoteJobRecord job, string error)
    {
        job.ErrorMessage = error;
        job.Status = QuoteJobStatus.Failed;
    }

    public string OutputPath(string fileName) => Path.Combine(outputDirectory, fileName);
}

public enum QuoteJobStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}

public sealed class QuoteJobRecord(
    Guid id,
    string quoteNumber,
    CreateQuoteRequest request,
    QuoteJobStatus status,
    DateTimeOffset createdAt,
    string? outputFileName,
    string? errorMessage,
    int attempt)
{
    public Guid Id { get; } = id;
    public string QuoteNumber { get; } = quoteNumber;
    public CreateQuoteRequest Request { get; } = request;
    public QuoteJobStatus Status { get; set; } = status;
    public DateTimeOffset CreatedAt { get; } = createdAt;
    public string? OutputFileName { get; set; } = outputFileName;
    public string? ErrorMessage { get; set; } = errorMessage;
    public int Attempt { get; set; } = attempt;
}
