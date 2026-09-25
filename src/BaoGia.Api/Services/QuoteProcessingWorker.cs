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
        var rows = new StringBuilder();
        decimal total = 0;
        foreach (var item in job.Request.Items)
        {
            var lineTotal = item.Quantity * item.UnitPrice;
            total += lineTotal;
            rows.Append($"<tr><td>{HtmlEncoder.Default.Encode(item.Product)}</td><td>{item.Quantity}</td><td>{item.UnitPrice:N0}</td><td>{lineTotal:N0}</td></tr>");
        }

        return $$"""
            <!doctype html><html lang="vi"><head><meta charset="utf-8"><title>Bao gia {{job.QuoteNumber}}</title>
            <style>body{font-family:Arial;margin:48px;color:#18222d}h1{color:#0b6e69}table{border-collapse:collapse;width:100%;margin-top:24px}th,td{border:1px solid #ccd6d5;padding:10px;text-align:left}th{background:#e5f2f0}.total{text-align:right;font-size:20px;font-weight:bold}</style></head>
            <body><h1>AN BINH CHEMTECH</h1><h2>BAO GIA {{job.QuoteNumber}}</h2><p><b>Khach hang:</b> {{HtmlEncoder.Default.Encode(job.Request.CustomerName)}}</p>
            <table><thead><tr><th>San pham</th><th>So luong</th><th>Don gia</th><th>Thanh tien</th></tr></thead><tbody>{{rows}}</tbody></table>
            <p class="total">Tong cong: {{total:N0}} VND</p><p><b>Thanh toan:</b> {{HtmlEncoder.Default.Encode(job.Request.PaymentTerms)}}</p><p><b>Giao hang:</b> {{HtmlEncoder.Default.Encode(job.Request.DeliveryTerms)}}</p></body></html>
            """;
    }
}
