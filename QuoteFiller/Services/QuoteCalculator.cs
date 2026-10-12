using QuoteFiller.Models;

namespace QuoteFiller.Services;

public class CalculationOptions
{
    public decimal VatRate { get; set; } = 0.08m; // Mặc định 8% (0.08)
}

public class QuoteCalculator
{
    private readonly decimal _vatRate;

    public QuoteCalculator(decimal vatRate = 0.08m)
    {
        _vatRate = vatRate;
    }

    public QuoteCalculator(CalculationOptions options)
    {
        _vatRate = options?.VatRate ?? 0.08m;
    }

    public decimal VatRate => _vatRate;

    /// <summary>
    /// Tính giá đã bao gồm VAT theo công thức xác định: round(price_excl_vat * (1 + vatRate))
    /// </summary>
    public decimal CalculatePriceInclVat(decimal priceExclVat)
    {
        return Math.Round(priceExclVat * (1 + _vatRate), 0, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Chuyển đổi dữ liệu trích xuất từ AI (AiMappedQuote) thành QuoteRequest sẵn sàng điền vào PPTX.
    /// Toàn bộ phép tính VAT được tính toán tại đây bằng mã C# xác định.
    /// </summary>
    public QuoteRequest BuildQuoteRequest(AiMappedQuote aiMapped, string revision = "01")
    {
        var request = new QuoteRequest
        {
            QuoteNumber = new QuoteNumberInfo
            {
                Sequence = aiMapped.Sequence,
                YearMonth = aiMapped.Period,
                Revision = revision
            },
            Customer = aiMapped.CustomerName,
            QuoteDate = $"2026-{aiMapped.Month.PadLeft(2, '0')}-{aiMapped.Day.PadLeft(2, '0')}",
            Items = new List<QuoteItem>()
        };

        foreach (var item in aiMapped.Items)
        {
            decimal inclVat = CalculatePriceInclVat(item.PriceExclVat);

            request.Items.Add(new QuoteItem
            {
                ProductName = item.Name,
                PriceWithoutVat = item.PriceExclVat,
                PriceWithVat = inclVat,
                VatRate = _vatRate
            });
        }

        return request;
    }

    /// <summary>
    /// Kiểm tra lại sau khi tính toán và sau khi điền: giá đã VAT phải khớp 100% công thức.
    /// </summary>
    public bool VerifyCalculations(QuoteRequest request, out List<string> errors)
    {
        errors = new List<string>();

        foreach (var item in request.Items)
        {
            decimal expected = CalculatePriceInclVat(item.PriceWithoutVat);
            if (item.CalculatedPriceWithVat != expected)
            {
                errors.Add($"Sai lệch tính toán tại sản phẩm '{item.ProductName}': " +
                           $"Giá chưa VAT={item.PriceWithoutVat}, Giá có VAT thực tế={item.CalculatedPriceWithVat}, Kỳ vọng={expected} (VAT rate={_vatRate})");
            }
        }

        return errors.Count == 0;
    }
}
