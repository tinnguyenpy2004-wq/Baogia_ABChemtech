namespace BaoGia.Domain.Quotes;

public enum QuoteJobStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}

public sealed class QuoteJob
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string QuoteNumber { get; init; }
    public required string IdempotencyKey { get; init; }
    public QuoteJobStatus Status { get; private set; } = QuoteJobStatus.Pending;
    public int Attempt { get; private set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LockedUntil { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }

    public void Start(TimeSpan lease)
    {
        Status = QuoteJobStatus.Processing;
        Attempt++;
        LockedUntil = DateTimeOffset.UtcNow.Add(lease);
    }

    public void Complete()
    {
        Status = QuoteJobStatus.Completed;
        LockedUntil = null;
    }

    public void Fail(string code, string message)
    {
        Status = QuoteJobStatus.Failed;
        ErrorCode = code;
        ErrorMessage = message;
        LockedUntil = null;
    }
}
