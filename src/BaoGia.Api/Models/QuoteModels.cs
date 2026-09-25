using System.ComponentModel.DataAnnotations;

namespace BaoGia.Api.Models;

public sealed record QuoteLineRequest
{
    [Required, MaxLength(120)]
    public required string Product { get; init; }

    [Range(1, 1_000_000)]
    public int Quantity { get; init; }

    [Range(0.01, 100000000000.0)]
    public decimal UnitPrice { get; init; }
}

public sealed record CreateQuoteRequest
{
    [Required, MaxLength(120)]
    public required string CustomerId { get; init; }

    [Required, MaxLength(120)]
    public required string CustomerName { get; init; }

    [Required, MinLength(1), MaxLength(20)]
    public required IReadOnlyList<QuoteLineRequest> Items { get; init; }

    [Required, MaxLength(500)]
    public required string PaymentTerms { get; init; }

    [Required, MaxLength(500)]
    public required string DeliveryTerms { get; init; }

    [Required, MaxLength(100)]
    public required string IdempotencyKey { get; init; }
}

public sealed record QuoteResponse(
    Guid Id,
    string QuoteNumber,
    string CustomerName,
    string Status,
    decimal Total,
    DateTimeOffset CreatedAt,
    string? ErrorMessage,
    string? DownloadUrl,
    string? ProductName = null);
