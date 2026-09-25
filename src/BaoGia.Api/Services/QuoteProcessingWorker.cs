using System.Net;
using System.Text.Encodings.Web;
using System.Text;
using BaoGia.Api.Models;

namespace BaoGia.Api.Services;

public sealed class QuoteProcessingWorker(
    QuoteJobStore store,
    ILogger<QuoteProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (store.TryClaim(out var job) && job is not null)
            {
                try
                {
                    await Task.Delay(500, stoppingToken); // Mo phong worker/Mac mini xu ly.
                    var fileName = $"{job.QuoteNumber}.html";
                    await File.WriteAllTextAsync(store.OutputPath(fileName), RenderQuote(job), stoppingToken);
                    store.Complete(job, fileName);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    store.Fail(job, exception.Message);
                    logger.LogError(exception, "Khong the tao bao gia {QuoteNumber}", job.QuoteNumber);
                }
            }

            await Task.Delay(250, stoppingToken);
        }
    }

    private static string RenderQuote(QuoteJobRecord job)
    {
        var culture = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");
        var rows = new StringBuilder();
        decimal subtotal = 0;
        int index = 1;

        foreach (var item in job.Request.Items)
        {
            var lineTotal = item.Quantity * item.UnitPrice;
            subtotal += lineTotal;
            rows.Append($"""
                <tr>
                    <td class="text-center">{index++}</td>
                    <td class="product-name"><strong>{HtmlEncoder.Default.Encode(item.Product)}</strong></td>
                    <td class="text-center">Đơn vị / Phuy</td>
                    <td class="text-center">{item.Quantity:N0}</td>
                    <td class="text-right">{item.UnitPrice.ToString("N0", culture)} ₫</td>
                    <td class="text-right font-semibold">{lineTotal.ToString("N0", culture)} ₫</td>
                </tr>
                """);
        }

        var createdDate = job.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        var validUntil = job.CreatedAt.ToLocalTime().AddDays(30).ToString("dd/MM/yyyy");

        var template = GetTemplate();
        return template
            .Replace("{{QUOTE_NUMBER}}", HtmlEncoder.Default.Encode(job.QuoteNumber))
            .Replace("{{CREATED_DATE}}", createdDate)
            .Replace("{{VALID_UNTIL}}", validUntil)
            .Replace("{{CUSTOMER_NAME}}", HtmlEncoder.Default.Encode(job.Request.CustomerName))
            .Replace("{{CUSTOMER_ID}}", HtmlEncoder.Default.Encode(job.Request.CustomerId))
            .Replace("{{DELIVERY_TERMS}}", HtmlEncoder.Default.Encode(job.Request.DeliveryTerms))
            .Replace("{{PAYMENT_TERMS}}", HtmlEncoder.Default.Encode(job.Request.PaymentTerms))
            .Replace("{{ITEMS_ROWS}}", rows.ToString())
            .Replace("{{SUBTOTAL}}", subtotal.ToString("N0", culture))
            .Replace("{{TOTAL}}", subtotal.ToString("N0", culture));
    }

    private static string GetTemplate()
    {
        var possiblePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "templates", "quote-template.html"),
            Path.Combine(Directory.GetCurrentDirectory(), "templates", "quote-template.html"),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "BaoGia.Api", "templates", "quote-template.html"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "templates", "quote-template.html")
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                return File.ReadAllText(path, Encoding.UTF8);
            }
        }

        return FallbackTemplate;
    }

    private const string FallbackTemplate = """
        <!doctype html>
        <html lang="vi">
        <head>
          <meta charset="utf-8">
          <title>Báo giá {{QUOTE_NUMBER}} - An Bình Chemtech</title>
          <style>body{font-family:Arial,sans-serif;margin:40px;color:#1e293b}table{width:100%;border-collapse:collapse}th,td{border:1px solid #cbd5e1;padding:10px}th{background:#0f766e;color:#fff}.text-right{text-align:right}.text-center{text-align:center}</style>
        </head>
        <body>
          <h2>CÔNG TY CP AN BÌNH CHEMTECH</h2>
          <h3>BÁO GIÁ: {{QUOTE_NUMBER}}</h3>
          <p><strong>Khách hàng:</strong> {{CUSTOMER_NAME}} ({{CUSTOMER_ID}})</p>
          <p><strong>Ngày lập:</strong> {{CREATED_DATE}} | <strong>Hiệu lực đến:</strong> {{VALID_UNTIL}}</p>
          <table>
            <thead><tr><th class="text-center">STT</th><th>Tên sản phẩm</th><th class="text-center">ĐVT</th><th class="text-center">Số lượng</th><th class="text-right">Đơn giá</th><th class="text-right">Thành tiền</th></tr></thead>
            <tbody>{{ITEMS_ROWS}}</tbody>
          </table>
          <p class="text-right" style="font-size:18px;font-weight:bold;margin-top:16px;">TỔNG CỘNG: {{TOTAL}} VND</p>
          <p><strong>Thanh toán:</strong> {{PAYMENT_TERMS}}</p>
          <p><strong>Giao hàng:</strong> {{DELIVERY_TERMS}}</p>
        </body>
        </html>
        """;
}
