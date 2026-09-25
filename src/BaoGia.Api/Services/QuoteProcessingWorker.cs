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

        return $$"""
            <!doctype html>
            <html lang="vi">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1.0">
              <title>Báo giá {{job.QuoteNumber}} - An Bình Chemtech</title>
              <link rel="preconnect" href="https://fonts.googleapis.com">
              <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
              <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap" rel="stylesheet">
              <style>
                :root {
                  --primary: #0f766e;
                  --primary-dark: #115e59;
                  --primary-light: #ccfbf1;
                  --text-main: #0f172a;
                  --text-muted: #475569;
                  --border-color: #cbd5e1;
                  --bg-light: #f8fafc;
                }
                * { box-sizing: border-box; margin: 0; padding: 0; }
                body {
                  font-family: 'Inter', system-ui, -apple-system, sans-serif;
                  background: #f1f5f9;
                  color: var(--text-main);
                  padding: 40px 20px;
                  line-height: 1.5;
                  -webkit-print-color-adjust: exact;
                  print-color-adjust: exact;
                }
                .invoice-card {
                  max-width: 860px;
                  margin: 0 auto;
                  background: #ffffff;
                  border-radius: 12px;
                  box-shadow: 0 10px 25px -5px rgba(0, 0, 0, 0.05), 0 8px 10px -6px rgba(0, 0, 0, 0.01);
                  padding: 48px;
                  border: 1px solid #e2e8f0;
                }
                .header {
                  display: flex;
                  justify-content: space-between;
                  align-items: flex-start;
                  border-bottom: 2px solid var(--primary-light);
                  padding-bottom: 28px;
                  margin-bottom: 28px;
                }
                .brand-title {
                  font-size: 24px;
                  font-weight: 700;
                  color: var(--primary);
                  letter-spacing: -0.5px;
                  text-transform: uppercase;
                }
                .brand-sub {
                  font-size: 13px;
                  color: var(--text-muted);
                  margin-top: 4px;
                  line-height: 1.4;
                }
                .quote-meta-badge {
                  text-align: right;
                }
                .quote-badge-title {
                  font-size: 20px;
                  font-weight: 700;
                  color: var(--primary-dark);
                  letter-spacing: 0.5px;
                }
                .quote-no {
                  font-size: 14px;
                  font-weight: 600;
                  color: var(--primary);
                  background: var(--primary-light);
                  display: inline-block;
                  padding: 4px 10px;
                  border-radius: 6px;
                  margin-top: 6px;
                }
                .quote-date {
                  font-size: 13px;
                  color: var(--text-muted);
                  margin-top: 6px;
                }
                .grid-info {
                  display: grid;
                  grid-template-columns: 1fr 1fr;
                  gap: 32px;
                  margin-bottom: 32px;
                }
                .info-box {
                  background: var(--bg-light);
                  border: 1px solid #e2e8f0;
                  border-radius: 8px;
                  padding: 18px 20px;
                }
                .info-box h3 {
                  font-size: 12px;
                  text-transform: uppercase;
                  letter-spacing: 0.05em;
                  color: var(--primary);
                  font-weight: 700;
                  margin-bottom: 12px;
                  border-bottom: 1px solid #e2e8f0;
                  padding-bottom: 6px;
                }
                .info-row {
                  font-size: 13px;
                  margin-bottom: 6px;
                  display: flex;
                }
                .info-label {
                  width: 110px;
                  flex-shrink: 0;
                  color: var(--text-muted);
                }
                .info-value {
                  font-weight: 500;
                  color: var(--text-main);
                }
                table.items-table {
                  width: 100%;
                  border-collapse: collapse;
                  margin-top: 8px;
                  margin-bottom: 28px;
                  font-size: 13.5px;
                }
                table.items-table th {
                  background: var(--primary);
                  color: #ffffff;
                  font-weight: 600;
                  padding: 12px 14px;
                  text-align: left;
                  font-size: 13px;
                }
                table.items-table th:first-child { border-top-left-radius: 6px; }
                table.items-table th:last-child { border-top-right-radius: 6px; }
                table.items-table td {
                  padding: 12px 14px;
                  border-bottom: 1px solid #e2e8f0;
                  color: #1e293b;
                }
                table.items-table tbody tr:hover {
                  background: #f8fafc;
                }
                .text-center { text-align: center; }
                .text-right { text-align: right; }
                .font-semibold { font-weight: 600; }
                .total-section {
                  display: flex;
                  justify-content: flex-end;
                  margin-bottom: 32px;
                }
                .total-card {
                  width: 340px;
                  background: var(--bg-light);
                  border: 1px solid #cbd5e1;
                  border-radius: 8px;
                  padding: 16px 20px;
                }
                .total-row {
                  display: flex;
                  justify-content: space-between;
                  font-size: 13.5px;
                  margin-bottom: 8px;
                  color: var(--text-muted);
                }
                .total-row.grand-total {
                  border-top: 2px dashed #cbd5e1;
                  padding-top: 10px;
                  margin-top: 10px;
                  font-size: 16px;
                  font-weight: 700;
                  color: var(--primary-dark);
                }
                .terms-section {
                  background: #fff;
                  border: 1px solid #e2e8f0;
                  border-left: 4px solid var(--primary);
                  border-radius: 4px;
                  padding: 16px 20px;
                  margin-bottom: 36px;
                }
                .terms-title {
                  font-size: 13px;
                  font-weight: 700;
                  color: var(--primary-dark);
                  margin-bottom: 8px;
                  text-transform: uppercase;
                }
                .terms-item {
                  font-size: 13px;
                  color: var(--text-muted);
                  margin-bottom: 4px;
                }
                .terms-item strong {
                  color: var(--text-main);
                }
                .signatures {
                  display: grid;
                  grid-template-columns: 1fr 1fr;
                  gap: 32px;
                  margin-top: 40px;
                  text-align: center;
                }
                .sig-box {
                  padding-top: 10px;
                }
                .sig-role {
                  font-size: 13px;
                  font-weight: 700;
                  color: var(--text-main);
                  text-transform: uppercase;
                }
                .sig-sub {
                  font-size: 12px;
                  color: var(--text-muted);
                  font-style: italic;
                  margin-top: 2px;
                }
                .sig-space {
                  height: 80px;
                }
                .sig-name {
                  font-size: 14px;
                  font-weight: 600;
                  color: var(--primary-dark);
                }
                .footer {
                  margin-top: 40px;
                  text-align: center;
                  font-size: 12px;
                  color: #94a3b8;
                  border-top: 1px solid #f1f5f9;
                  padding-top: 16px;
                }
                @media print {
                  body { background: #fff; padding: 0; }
                  .invoice-card { box-shadow: none; border: none; padding: 0; max-width: 100%; }
                }
              </style>
            </head>
            <body>
              <div class="invoice-card">
                <div class="header">
                  <div>
                    <div class="brand-title">CÔNG TY CỔ PHẦN AN BÌNH CHEMTECH</div>
                    <div class="brand-sub">
                      Trụ sở: Lô B3, Đường Hóa Chất, KCN Hiệp Phước, TP. Hồ Chí Minh<br>
                      Hotline: (+84) 28 3822 xxxx | MST: 0314892749 | Email: sales@anbinhchemtech.vn
                    </div>
                  </div>
                  <div class="quote-meta-badge">
                    <div class="quote-badge-title">BẢNG BÁO GIÁ</div>
                    <div class="quote-no">{{job.QuoteNumber}}</div>
                    <div class="quote-date">Ngày lập: {{createdDate}}</div>
                    <div class="quote-date">Hiệu lực đến: {{validUntil}}</div>
                  </div>
                </div>

                <div class="grid-info">
                  <div class="info-box">
                    <h3>Thông tin khách hàng</h3>
                    <div class="info-row">
                      <span class="info-label">Khách hàng:</span>
                      <span class="info-value">{{HtmlEncoder.Default.Encode(job.Request.CustomerName)}}</span>
                    </div>
                    <div class="info-row">
                      <span class="info-label">Mã khách hàng:</span>
                      <span class="info-value">{{HtmlEncoder.Default.Encode(job.Request.CustomerId)}}</span>
                    </div>
                    <div class="info-row">
                      <span class="info-label">Địa điểm giao:</span>
                      <span class="info-value">{{HtmlEncoder.Default.Encode(job.Request.DeliveryTerms)}}</span>
                    </div>
                  </div>

                  <div class="info-box">
                    <h3>Đơn vị phát hành</h3>
                    <div class="info-row">
                      <span class="info-label">Đơn vị:</span>
                      <span class="info-value">Phòng Kinh Doanh Hóa Chất Công Nghiệp</span>
                    </div>
                    <div class="info-row">
                      <span class="info-label">Hệ thống:</span>
                      <span class="info-value">Bao Gia Automation Engine v1.0</span>
                    </div>
                    <div class="info-row">
                      <span class="info-label">Trạng thái:</span>
                      <span class="info-value" style="color: #0f766e; font-weight: 600;">ĐÃ XÁC NHẬN</span>
                    </div>
                  </div>
                </div>

                <table class="items-table">
                  <thead>
                    <tr>
                      <th class="text-center" style="width: 50px;">STT</th>
                      <th>Tên sản phẩm / Danh mục hóa chất</th>
                      <th class="text-center" style="width: 110px;">ĐVT</th>
                      <th class="text-center" style="width: 90px;">Số lượng</th>
                      <th class="text-right" style="width: 140px;">Đơn giá</th>
                      <th class="text-right" style="width: 150px;">Thành tiền</th>
                    </tr>
                  </thead>
                  <tbody>
                    {{rows}}
                  </tbody>
                </table>

                <div class="total-section">
                  <div class="total-card">
                    <div class="total-row">
                      <span>Cộng tiền hàng:</span>
                      <span>{{subtotal.ToString("N0", culture)}} ₫</span>
                    </div>
                    <div class="total-row">
                      <span>Thuế GTGT (VAT):</span>
                      <span>Bao gồm theo quy định</span>
                    </div>
                    <div class="total-row grand-total">
                      <span>TỔNG CỘNG:</span>
                      <span>{{subtotal.ToString("N0", culture)}} VND</span>
                    </div>
                  </div>
                </div>

                <div class="terms-section">
                  <div class="terms-title">Điều khoản & Điều kiện thương mại</div>
                  <div class="terms-item"><strong>1. Điều khoản thanh toán:</strong> {{HtmlEncoder.Default.Encode(job.Request.PaymentTerms)}}.</div>
                  <div class="terms-item"><strong>2. Thời gian & phương thức giao hàng:</strong> {{HtmlEncoder.Default.Encode(job.Request.DeliveryTerms)}}.</div>
                  <div class="terms-item"><strong>3. Tiêu chuẩn chất lượng:</strong> Hàng hóa có đầy đủ CO/CQ, MSDS và chứng chỉ phân tích chất lượng từ nhà máy.</div>
                  <div class="terms-item"><strong>4. Hiệu lực báo giá:</strong> Có giá trị trong vòng 30 ngày kể từ ngày ban hành.</div>
                </div>

                <div class="signatures">
                  <div class="sig-box">
                    <div class="sig-role">Đại diện khách hàng</div>
                    <div class="sig-sub">(Ký, ghi rõ họ tên và đóng dấu)</div>
                    <div class="sig-space"></div>
                    <div class="sig-name">Xác nhận đặt hàng</div>
                  </div>
                  <div class="sig-box">
                    <div class="sig-role">CÔNG TY CP AN BÌNH CHEMTECH</div>
                    <div class="sig-sub">(Người lập báo giá / Giám đốc kinh doanh)</div>
                    <div class="sig-space"></div>
                    <div class="sig-name">Phòng Kinh Doanh Chemtech</div>
                  </div>
                </div>

                <div class="footer">
                  Tài liệu này được tạo tự động bởi Hệ thống Tự động hóa Báo giá An Bình Chemtech (BaoGia Automation Prototype).
                </div>
              </div>
            </body>
            </html>
            """;
    }
}
