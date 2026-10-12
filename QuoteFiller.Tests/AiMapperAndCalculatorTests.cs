using QuoteFiller.Services;
using Xunit;

namespace QuoteFiller.Tests;

public class AiMapperAndCalculatorTests
{
    [Fact]
    public void AiMapper_MockExtract_ShouldHandleRawVietnameseFields()
    {
        string rawLaravelJson = @"{
            ""ten_khach_hang"": ""Công ty TNHH Thực Phẩm Sao Mai"",
            ""quote_no"": ""0088"",
            ""period"": ""2606"",
            ""ngay_bao_gia"": ""2026-06-28"",
            ""items"": [
                {
                    ""ten_san_pham"": ""Sodium Tripolyphosphate (STPP)"",
                    ""don_gia_chua_vat"": 51560
                }
            ]
        }";

        var mapped = AiMapper.MockExtract(rawLaravelJson);

        Assert.Equal("Công ty TNHH Thực Phẩm Sao Mai", mapped.CustomerName);
        Assert.Equal("0088", mapped.Sequence);
        Assert.Equal("2606", mapped.Period);
        Assert.Equal("28", mapped.Day);
        Assert.Equal("06", mapped.Month);
        Assert.Single(mapped.Items);
        Assert.Equal("Sodium Tripolyphosphate (STPP)", mapped.Items[0].Name);
        Assert.Equal(51560m, mapped.Items[0].PriceExclVat);

        var err = AiMapper.Validate(mapped);
        Assert.Null(err);
    }

    [Fact]
    public void QuoteCalculator_ShouldCalculateAccurateVatAndPassVerification()
    {
        var calculator = new QuoteCalculator(0.08m); // 8% VAT
        decimal priceWithoutVat = 51560m;
        decimal expectedWithVat = 55685m; // round(51560 * 1.08) = 55684.8 -> 55685

        decimal actualWithVat = calculator.CalculatePriceInclVat(priceWithoutVat);
        Assert.Equal(expectedWithVat, actualWithVat);

        var aiMapped = new AiMappedQuote
        {
            CustomerName = "Công ty Cổ phần Sữa Việt Nam",
            Period = "2606",
            Sequence = "0002",
            Day = "25",
            Month = "06",
            Items = new List<AiMappedItem>
            {
                new() { Name = "Tetrasodium Pyrophosphate (TSPP)", PriceExclVat = 51560m }
            }
        };

        var request = calculator.BuildQuoteRequest(aiMapped);
        bool isValid = calculator.VerifyCalculations(request, out var errors);

        Assert.True(isValid);
        Assert.Empty(errors);
        Assert.Equal(expectedWithVat, request.Items[0].PriceWithVat);
    }

    [Fact]
    public void AiMapper_Validate_ShouldCatchInvalidPriceAndDate()
    {
        var invalidQuote = new AiMappedQuote
        {
            CustomerName = "Khách hàng Test",
            Period = "2606",
            Sequence = "0001",
            Day = "40", // Ngày không hợp lệ
            Month = "06",
            Items = new List<AiMappedItem>
            {
                new() { Name = "Sản phẩm A", PriceExclVat = -100m } // Giá âm
            }
        };

        var error = AiMapper.Validate(invalidQuote);
        Assert.NotNull(error);
        Assert.Contains("Ngày", error);
    }
}
