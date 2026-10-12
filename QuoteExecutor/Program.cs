using System.Text.Json;
using QuoteExecutor.Models;
using QuoteExecutor.Services;
using QuoteFiller.Services;

var builder = WebApplication.CreateBuilder(args);

// Cấu hình lắng nghe trên mọi card mạng (bao gồm IP Tailscale) nếu chưa chỉ định URL
string urls = builder.Configuration["ASPNETCORE_URLS"] ?? builder.Configuration["Urls"] ?? "http://0.0.0.0:5000";
builder.WebHost.UseUrls(urls);

// Đăng ký các dịch vụ
builder.Services.AddSingleton<IQuoteJobQueue, QuoteJobQueue>();
builder.Services.AddHttpClient();

// Đăng ký AiMapper và QuoteCalculator
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var options = new AiMapperOptions
    {
        ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? config["AiMapper:ApiKey"] ?? "",
        Model = config["AiMapper:Model"] ?? "claude-3-5-sonnet-20241022",
        MockAi = string.Equals(Environment.GetEnvironmentVariable("MOCK_AI"), "true", StringComparison.OrdinalIgnoreCase)
                 || config.GetValue<bool>("AiMapper:MockAi", true)
    };
    return new AiMapper(httpClientFactory.CreateClient(), options);
});

builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    decimal vatRate = config.GetValue<decimal>("VatRate", 0.08m);
    return new QuoteCalculator(vatRate);
});

// Đăng ký BackgroundService xử lý tuần tự từng job qua Channel
builder.Services.AddHostedService<QuoteBackgroundService>();

var app = builder.Build();

// Helper kiểm tra API Key
bool IsAuthorized(HttpContext context, IConfiguration config)
{
    string expectedToken = Environment.GetEnvironmentVariable("EXECUTOR_TOKEN") 
                           ?? config["ExecutorToken"] 
                           ?? "anbinh-chemtech-secret-token";

    if (!context.Request.Headers.TryGetValue("X-API-Key", out var providedKey))
    {
        return false;
    }
    return string.Equals(providedKey.ToString(), expectedToken, StringComparison.Ordinal);
}

// 1. Endpoint kiểm tra sức khỏe hệ thống
app.MapGet("/", () => Results.Ok(new
{
    service = "An Binh Chemtech - Quote Executor Service",
    status = "healthy",
    version = "1.0.0",
    docs = "POST /quotes, GET /quotes/{id}, GET /quotes/{id}/file"
}));

// 2. POST /quotes: Tiếp nhận yêu cầu báo giá từ Laravel
app.MapPost("/quotes", async (HttpContext context, IQuoteJobQueue queue, IConfiguration config) =>
{
    if (!IsAuthorized(context, config))
    {
        return Results.Json(new { error = "Unauthorized. Header 'X-API-Key' không hợp lệ hoặc bị thiếu." }, statusCode: 401);
    }

    using var reader = new StreamReader(context.Request.Body);
    string rawJson = await reader.ReadToEndAsync();

    if (string.IsNullOrWhiteSpace(rawJson))
    {
        return Results.BadRequest(new { error = "Request body rỗng." });
    }

    // Sinh ID định danh duy nhất cho quote
    string shortId = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
    string quoteId = $"Q-{DateTime.UtcNow:yyMMdd}-{shortId}";
    string outputDir = config["OutputDir"] ?? "outputs";
    string outputPath = Path.Combine(Directory.GetCurrentDirectory(), outputDir, $"{quoteId}.pptx");

    var job = new QuoteJob
    {
        Id = quoteId,
        Status = "processing",
        RawJson = rawJson,
        OutputPath = outputPath,
        CreatedAt = DateTime.UtcNow
    };

    await queue.EnqueueAsync(job);

    return Results.Accepted($"/quotes/{quoteId}", new
    {
        quote_id = quoteId,
        status = "processing",
        message = "Yêu cầu đã được tiếp nhận và đưa vào hàng đợi xử lý nền."
    });
});

// 3. GET /quotes/{id}: Tra cứu trạng thái xử lý
app.MapGet("/quotes/{id}", (string id, HttpContext context, IQuoteJobQueue queue, IConfiguration config) =>
{
    if (!IsAuthorized(context, config))
    {
        return Results.Json(new { error = "Unauthorized. Header 'X-API-Key' không hợp lệ." }, statusCode: 401);
    }

    var job = queue.GetJob(id);
    if (job == null)
    {
        return Results.NotFound(new { error = $"Không tìm thấy báo giá với ID: {id}" });
    }

    return Results.Ok(new
    {
        quote_id = job.Id,
        status = job.Status,
        error = job.ErrorMessage,
        created_at = job.CreatedAt,
        completed_at = job.CompletedAt,
        download_url = job.Status == "done" ? $"/quotes/{job.Id}/file" : null,
        logs = job.Logs
    });
});

// 4. GET /quotes/{id}/file: Tải file PPTX hoàn chỉnh
app.MapGet("/quotes/{id}/file", (string id, HttpContext context, IQuoteJobQueue queue, IConfiguration config) =>
{
    if (!IsAuthorized(context, config))
    {
        return Results.Json(new { error = "Unauthorized. Header 'X-API-Key' không hợp lệ." }, statusCode: 401);
    }

    var job = queue.GetJob(id);
    if (job == null)
    {
        return Results.NotFound(new { error = $"Không tìm thấy báo giá với ID: {id}" });
    }

    if (job.Status == "processing")
    {
        return Results.Json(new { status = "processing", message = "File đang được xử lý, vui lòng thử lại sau vài giây." }, statusCode: 425);
    }

    if (job.Status == "failed" || !File.Exists(job.OutputPath))
    {
        return Results.BadRequest(new { error = "Tạo file thất bại.", details = job.ErrorMessage });
    }

    const string pptxContentType = "application/vnd.openxmlformats-officedocument.presentationml.presentation";
    return Results.File(job.OutputPath, contentType: pptxContentType, fileDownloadName: $"{job.Id}.pptx");
});

Console.WriteLine($"[INFO] Quote Executor đang lắng nghe tại: {urls}");
app.Run();
