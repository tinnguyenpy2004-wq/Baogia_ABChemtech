using System.Text.Json.Serialization;

namespace QuoteFiller.Models;

public class MappingConfig
{
    [JsonPropertyName("quoteNumberShape")]
    public string QuoteNumberShape { get; set; } = "txt_so_bao_gia";

    [JsonPropertyName("customerShape")]
    public string CustomerShape { get; set; } = "txt_ten_khach_hang";

    [JsonPropertyName("quoteDateShape")]
    public string QuoteDateShape { get; set; } = "txt_ngay_thang_bao_gia";

    [JsonPropertyName("shapeNameAliases")]
    public Dictionary<string, List<string>> ShapeNameAliases { get; set; } = new();

    [JsonPropertyName("items")]
    public List<ItemShapeMapping> Items { get; set; } = new();
}

public class ItemShapeMapping
{
    [JsonPropertyName("nameShape")]
    public string NameShape { get; set; } = string.Empty;

    [JsonPropertyName("priceWithoutVatShape")]
    public string PriceWithoutVatShape { get; set; } = string.Empty;

    [JsonPropertyName("priceWithVatShape")]
    public string PriceWithVatShape { get; set; } = string.Empty;
}
