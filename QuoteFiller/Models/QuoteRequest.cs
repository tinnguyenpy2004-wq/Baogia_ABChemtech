using System.Text.Json.Serialization;

namespace QuoteFiller.Models;

public class QuoteRequest
{
    [JsonPropertyName("quoteNumber")]
    public QuoteNumberInfo? QuoteNumber { get; set; }

    [JsonPropertyName("customer")]
    public string? Customer { get; set; }

    [JsonPropertyName("quoteDate")]
    public string? QuoteDate { get; set; } // Định dạng YYYY-MM-DD hoặc DD/MM/YYYY

    [JsonPropertyName("items")]
    public List<QuoteItem> Items { get; set; } = new();
}

public class QuoteNumberInfo
{
    [JsonPropertyName("sequence")]
    public string Sequence { get; set; } = "0001";

    [JsonPropertyName("yearMonth")]
    public string YearMonth { get; set; } = "2606";

    [JsonPropertyName("revision")]
    public string Revision { get; set; } = "01";
}

public class QuoteItem
{
    [JsonPropertyName("productName")]
    public string ProductName { get; set; } = string.Empty;

    [JsonPropertyName("specification")]
    public string? Specification { get; set; } // Mặc định 25 kg/bao

    [JsonPropertyName("priceWithoutVat")]
    public decimal PriceWithoutVat { get; set; }

    [JsonPropertyName("priceWithVat")]
    public decimal? PriceWithVat { get; set; }

    [JsonPropertyName("vatRate")]
    public decimal? VatRate { get; set; } // Ví dụ 0.08 hoặc 0.10

    public decimal CalculatedPriceWithVat
    {
        get
        {
            if (PriceWithVat.HasValue) return PriceWithVat.Value;
            if (VatRate.HasValue) return Math.Round(PriceWithoutVat * (1 + VatRate.Value), 0);
            return PriceWithoutVat;
        }
    }
}
