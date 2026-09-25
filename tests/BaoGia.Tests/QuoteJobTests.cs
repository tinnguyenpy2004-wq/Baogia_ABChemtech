using BaoGia.Domain.Quotes;

namespace BaoGia.Tests;

public sealed class QuoteJobTests
{
    [Fact]
    public void StartThenCompleteMovesJobThroughExpectedStates()
    {
        var job = new QuoteJob
        {
            QuoteNumber = "BG-2026-0001",
            IdempotencyKey = "test-key"
        };

        job.Start(TimeSpan.FromMinutes(5));

        Assert.Equal(QuoteJobStatus.Processing, job.Status);
        Assert.Equal(1, job.Attempt);
        Assert.True(job.LockedUntil > DateTimeOffset.UtcNow);

        job.Complete();

        Assert.Equal(QuoteJobStatus.Completed, job.Status);
        Assert.Null(job.LockedUntil);
    }
}
