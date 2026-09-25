namespace BaoGia.Domain.Quotes;

public static class QuoteJobStatusRules
{
    public static bool CanRetry(QuoteJob job, int maxAttempts) =>
        job.Status == QuoteJobStatus.Processing && job.Attempt < maxAttempts;
}
