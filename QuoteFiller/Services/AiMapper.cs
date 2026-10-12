using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace QuoteFiller.Services;

public class AiMappedQuote
{
    [JsonPropertyName("customer_code")]
    public string? CustomerCode { get; set; }

    [JsonPropertyName("period")]
    public string Period { get; set; } = string.Empty;

    [JsonPropertyName("sequence")]
    public string Sequence { get; set; } = string.Empty;

    [JsonPropertyName("customer_name")]
    public string CustomerName { get; set; } = string.Empty;

    [JsonPropertyName("day")]
    public string Day { get; set; } = string.Empty;

    [JsonPropertyName("month")]
    public string Month { get; set; } = string.Empty;

    [JsonPropertyName("items")]
    public List<AiMappedItem> Items { get; set; } = new();
}

public class AiMappedItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("price_excl_vat")]
    public decimal PriceExclVat { get; set; }
}

public class AiMapperOptions
{
    public string ApiKey { get; set; } = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? string.Empty;
    public string Model { get; set; } = "claude-3-5-sonnet-20241022";
    public bool MockAi { get; set; } = string.Equals(Environment.GetEnvironmentVariable("MOCK_AI"), "true", StringComparison.OrdinalIgnoreCase);
}

public class AiMapper
{
    private readonly HttpClient _httpClient;
    private readonly AiMapperOptions _options;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AiMapper(HttpClient? httpClient = null, AiMapperOptions? options = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _options = options ?? new AiMapperOptions();
    }

    /// <summary>
    /// Tiếp nhận JSON thô từ Laravel và ánh xạ sang AiMappedQuote chuẩn bằng Anthropic API (hoặc Mock).
    /// Có cơ chế retry tối đa 2 lần nếu validate schema thất bại.
    /// </summary>
    public async Task<AiMappedQuote> MapRawRequestAsync(string rawJson, string? mappingReference = null, CancellationToken cancellationToken = default)
    {
        // 1. Chế độ Mock AI (khi không có key hoặc MOCK_AI=true)
        if (_options.MockAi || string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return MockExtract(rawJson);
        }

        // 2. Chế độ AI với Anthropic API kèm retry tối đa 2 lần
        int maxRetries = 2;
        string? previousError = null;

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            string systemPrompt = BuildSystemPrompt(mappingReference, previousError);
            string aiResponseText = await CallAnthropicApiAsync(rawJson, systemPrompt, cancellationToken);

            string cleanedJson = CleanJsonResponse(aiResponseText);

            try
            {
                var mapped = JsonSerializer.Deserialize<AiMappedQuote>(cleanedJson, JsonOptions);
                var validationError = Validate(mapped);

                if (string.IsNullOrEmpty(validationError))
                {
                    return mapped!;
                }

                // Ghi nhận lỗi để AI sửa ở lần thử kế tiếp
                previousError = validationError;
            }
            catch (Exception ex)
            {
                previousError = $"JSON deserialize failed: {ex.Message}. Response was: {cleanedJson}";
            }
        }

        throw new InvalidOperationException($"AI mapping thất bại sau 2 lần retry. Lỗi cuối: {previousError}");
    }

    private string BuildSystemPrompt(string? mappingReference, string? previousError)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are an expert AI data extraction agent for an ERP/Laravel quote generation system.");
        sb.AppendLine("Your ONLY task is to extract and normalize quote information from raw, messy input JSON into a STRICT JSON schema.");
        sb.AppendLine("CRITICAL RULES:");
        sb.AppendLine("1. Return ONLY the JSON object. Do NOT wrap in markdown fences (```). No explanations, no comments.");
        sb.AppendLine("2. Schema required:");
        sb.AppendLine("   {");
        sb.AppendLine("     \"customer_code\": string or null,");
        sb.AppendLine("     \"period\": string (e.g. \"2606\" representing YYMM or quotation period),");
        sb.AppendLine("     \"sequence\": string (e.g. \"0001\" quotation running sequence),");
        sb.AppendLine("     \"customer_name\": string (full customer company/person name),");
        sb.AppendLine("     \"day\": string (2 digits day, e.g. \"25\"),");
        sb.AppendLine("     \"month\": string (2 digits month, e.g. \"06\"),");
        sb.AppendLine("     \"items\": [ { \"name\": string, \"price_excl_vat\": number } ]");
        sb.AppendLine("   }");
        sb.AppendLine("3. DO NOT calculate VAT, DO NOT format currency with commas, DO NOT alter product price math.");
        sb.AppendLine("4. \"price_excl_vat\" MUST be a positive number.");

        if (!string.IsNullOrEmpty(mappingReference))
        {
            sb.AppendLine($"Target template mapping fields: {mappingReference}");
        }

        if (!string.IsNullOrEmpty(previousError))
        {
            sb.AppendLine($"[CORRECTION REQUEST]: Previous attempt failed validation with error: \"{previousError}\". Fix this strictly.");
        }

        return sb.ToString();
    }

    private async Task<string> CallAnthropicApiAsync(string userPrompt, string systemPrompt, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        request.Headers.Add("x-api-key", _options.ApiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        var payload = new
        {
            model = _options.Model,
            max_tokens = 1024,
            system = systemPrompt,
            messages = new[]
            {
                new { role = "user", content = $"Raw Laravel request:\n{userPrompt}" }
            }
        };

        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        string responseString = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Anthropic API trả về mã lỗi {response.StatusCode}: {responseString}");
        }

        using var doc = JsonDocument.Parse(responseString);
        var contentArray = doc.RootElement.GetProperty("content");
        if (contentArray.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("Anthropic API không trả về nội dung nào.");
        }

        return contentArray[0].GetProperty("text").GetString() ?? string.Empty;
    }

    public static string CleanJsonResponse(string rawResponse)
    {
        string text = rawResponse.Trim();

        // Bỏ markdown block ```json ... ``` hoặc ``` ... ```
        if (text.StartsWith("```"))
        {
            int firstNewLine = text.IndexOf('\n');
            if (firstNewLine > 0)
            {
                text = text.Substring(firstNewLine + 1);
            }
            if (text.EndsWith("```"))
            {
                text = text.Substring(0, text.Length - 3).Trim();
            }
        }

        // Tìm điểm bắt đầu '{' và kết thúc '}' nếu có ký tự thừa
        int startIdx = text.IndexOf('{');
        int endIdx = text.LastIndexOf('}');
        if (startIdx >= 0 && endIdx > startIdx)
        {
            text = text.Substring(startIdx, endIdx - startIdx + 1);
        }

        return text;
    }

    public static string? Validate(AiMappedQuote? quote)
    {
        if (quote == null) return "Dữ liệu báo giá rỗng.";
        if (string.IsNullOrWhiteSpace(quote.CustomerName)) return "Thiếu tên khách hàng (customer_name).";
        if (string.IsNullOrWhiteSpace(quote.Period)) return "Thiếu kỳ báo giá (period).";
        if (string.IsNullOrWhiteSpace(quote.Sequence)) return "Thiếu số thứ tự báo giá (sequence).";

        if (!int.TryParse(quote.Day, out int day) || day < 1 || day > 31)
            return $"Ngày '{quote.Day}' không hợp lệ (phải từ 1 đến 31).";

        if (!int.TryParse(quote.Month, out int month) || month < 1 || month > 12)
            return $"Tháng '{quote.Month}' không hợp lệ (phải từ 1 đến 12).";

        if (quote.Items == null || quote.Items.Count == 0)
            return "Danh sách sản phẩm (items) không được rỗng.";

        for (int i = 0; i < quote.Items.Count; i++)
        {
            var item = quote.Items[i];
            if (string.IsNullOrWhiteSpace(item.Name))
                return $"Sản phẩm #{i + 1} thiếu tên (name).";
            if (item.PriceExclVat <= 0)
                return $"Sản phẩm '{item.Name}' có đơn giá chưa VAT không hợp lệ (phải > 0).";
        }

        return null;
    }

    /// <summary>
    /// Bộ trích xuất mô phỏng (Mock Extract) dùng khi không có API key hoặc demo ngoại tuyến.
    /// Hỗ trợ cả schema chuẩn và các biến thể trường tiếng Việt / tiếng Anh thường gặp từ Laravel.
    /// </summary>
    public static AiMappedQuote MockExtract(string rawJson)
    {
        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;

        var result = new AiMappedQuote();

        // 1. Khách hàng
        result.CustomerName = GetStringProperty(root, "customer_name", "customer", "ten_khach_hang", "khach_hang", "client")
                              ?? "Khách hàng thân thiết";
        result.CustomerCode = GetStringProperty(root, "customer_code", "client_code", "ma_khach_hang");

        // 2. Số báo giá & Ngày tháng
        if (root.TryGetProperty("quoteNumber", out var qnElem))
        {
            result.Sequence = GetStringProperty(qnElem, "sequence", "so_thu_tu") ?? "0001";
            result.Period = GetStringProperty(qnElem, "yearMonth", "period", "ky") ?? "2606";
        }
        else
        {
            result.Sequence = GetStringProperty(root, "sequence", "so_thu_tu", "quote_no") ?? "0001";
            result.Period = GetStringProperty(root, "period", "yearMonth", "ky") ?? "2606";
        }

        // Tách ngày tháng
        string dateRaw = GetStringProperty(root, "quoteDate", "quote_date", "date", "ngay_bao_gia", "ngay") ?? "";
        if (DateTime.TryParse(dateRaw, out var dt))
        {
            result.Day = dt.Day.ToString("D2");
            result.Month = dt.Month.ToString("D2");
        }
        else
        {
            result.Day = GetStringProperty(root, "day", "ngay") ?? DateTime.Now.Day.ToString("D2");
            result.Month = GetStringProperty(root, "month", "thang") ?? DateTime.Now.Month.ToString("D2");
        }

        // 3. Danh sách sản phẩm
        if (root.TryGetProperty("items", out var itemsElem) && itemsElem.ValueKind == JsonValueKind.Array)
        {
            foreach (var itemElem in itemsElem.EnumerateArray())
            {
                string name = GetStringProperty(itemElem, "name", "productName", "product_name", "ten_hang", "ten_san_pham") ?? "Sản phẩm";
                decimal price = GetDecimalProperty(itemElem, "price_excl_vat", "priceWithoutVat", "price_without_vat", "don_gia_chua_vat", "gia_chua_vat", "price") ?? 50000;

                result.Items.Add(new AiMappedItem
                {
                    Name = name,
                    PriceExclVat = price
                });
            }
        }

        return result;
    }

    private static string? GetStringProperty(JsonElement element, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
                return prop.GetString();
        }
        return null;
    }

    private static decimal? GetDecimalProperty(JsonElement element, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (element.TryGetProperty(name, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out var val))
                    return val;
                if (prop.ValueKind == JsonValueKind.String && decimal.TryParse(prop.GetString(), out var sVal))
                    return sVal;
            }
        }
        return null;
    }
}
