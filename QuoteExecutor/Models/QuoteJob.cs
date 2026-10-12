namespace QuoteExecutor.Models;

public class QuoteJob
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = "processing"; // "processing", "done", "failed"
    public string RawJson { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public List<string> Logs { get; set; } = new();
}
