# TÀI LIỆU KỸ THUẬT VÀ GHI NHỚ TRIỂN KHAI (IMPLEMENTATION NOTES)
**Dự án**: AI Automation Engineer - Hệ thống tự động hóa tạo báo giá PowerPoint từ Laravel qua Tailscale  
**Công ty**: An Bình Chemtech  
**Công nghệ**: ASP.NET Core (.NET 8), Open XML SDK (`DocumentFormat.OpenXml` v3.5.1), System.Threading.Channels, Anthropic API (Claude 3.5 Sonnet)

---

## 1. Kiến trúc Tổng thể và Luồng dữ liệu (End-to-End Pipeline)

```
[Hệ thống Laravel]
       │
       │ HTTP POST /quotes (JSON thô, Header X-API-Key)
       ▼ (Mạng riêng ảo Tailscale 100.x.y.z:5000)
[QuoteExecutor API (ASP.NET Core Minimal API)]
       │
       │ 1. Xác thực X-API-Key với EXECUTOR_TOKEN
       │ 2. Sinh Job ID (vd: Q-261009-85DC6C)
       │ 3. Đẩy vào Channel<QuoteJob> (Trả 202 Accepted ngay lập tức)
       ▼
[QuoteBackgroundService (Background Queue Worker)]
       │ (Đọc tuần tự từng job qua Channel.Reader)
       │
       ├─► [Bước A: AiMapper]
       │       - Gọi Anthropic API (Model: claude-3-5-sonnet-20241022)
       │       - Prompt ép trả về DUY NHẤT JSON đúng schema
       │       - Code validate: đủ trường, giá > 0, ngày 1-31 (Retry max 2 lần)
       │       - Hỗ trợ chế độ Mock (MOCK_AI=true) khi offline
       │
       ├─► [Bước B: QuoteCalculator]
       │       - Code tính toán xác định 100%: price_incl_vat = round(price_excl_vat * (1 + vatRate))
       │       - vatRate cấu hình tại appsettings.json (mặc định 0.08)
       │       - Xác minh lại công thức trước và sau khi điền
       │
       ├─► [Bước C: QuoteFiller (Open XML SDK)]
       │       - Thao tác trên bản sao templates/quote-template.pptx
       │       - Thuật toán gộp Run XML: giữ nguyên font, cỡ chữ, màu sắc
       │       - Điền số báo giá 3 placeholder, khách hàng, ngày tháng, bảng giá
       │       - Quét toàn bộ slide: kiểm tra sạch 100% ngoặc vuông '[' ']'
       │
       ▼ Lưu file tại: outputs/{quote_id}.pptx
[Client Laravel Poll Status & Tải File]
       │ GET /quotes/{id}       --> status: "done"
       │ GET /quotes/{id}/file  --> Tải file .pptx hoàn chỉnh
```

---

## 2. Chi tiết các thành phần cốt lõi

### A. AiMapper (`QuoteFiller/Services/AiMapper.cs`)
- **Nhiệm vụ**: Chỉ trích xuất và chuẩn hóa dữ liệu thô. Tuyệt đối **không** để AI tính toán tiền hay sửa file PPTX.
- **Schema đầu ra**:
  ```json
  {
    "customer_code": "KH-0123",
    "period": "2606",
    "sequence": "0001",
    "customer_name": "Công ty Cổ phần Thực phẩm Á Châu (Asia Foods)",
    "day": "25",
    "month": "06",
    "items": [
      {
        "name": "Tetrasodium Pyrophosphate (TSPP)",
        "price_excl_vat": 51560
      }
    ]
  }
  ```
- **Xử lý an toàn**:
  - Tự động bóc tách markdown fences (```json ... ```) bằng hàm `CleanJsonResponse()`.
  - Validate nghiêm ngặt bằng code C#: nếu thiếu trường, giá $\le 0$ hoặc ngày tháng sai $\rightarrow$ gửi phản hồi lỗi kèm prompt yêu cầu AI sửa (retry tối đa 2 lần).
  - Tích hợp `MockExtract()` tự động kích hoạt khi biến môi trường `MOCK_AI=true` hoặc chưa thiết lập `ANTHROPIC_API_KEY`.

### B. QuoteCalculator (`QuoteFiller/Services/QuoteCalculator.cs`)
- **Nhiệm vụ**: Thực hiện 100% phép tính số học bằng code xác định (Deterministic Code).
- **Công thức**:
  $$\text{price\_incl\_vat} = \text{round}(\text{price\_excl\_vat} \times (1 + \text{vatRate}))$$
- **Kiểm định**: Hàm `VerifyCalculations` so khớp lại giá trị trước khi chuyển sang bước Open XML, đảm bảo số liệu hiển thị trên báo giá chuẩn xác tuyệt đối.

### C. QuoteExecutor API (`QuoteExecutor/`)
- **ASP.NET Core Minimal API** cấu hình lắng nghe trên mọi card mạng (`http://0.0.0.0:5000` hoặc bind IP Tailscale).
- **Bảo mật**: Header `X-API-Key` bắt buộc trên tất cả endpoint, so khớp với biến môi trường `EXECUTOR_TOKEN` (hoặc cấu hình trong `appsettings.json`).
- **Hàng đợi phi đồng bộ**: Dùng `System.Threading.Channels.Channel<QuoteJob>` kết hợp `BackgroundService` để xử lý lần lượt từng job, đảm bảo an toàn luồng và tối ưu tài nguyên CPU/RAM khi ghi file Open XML.

---

## 3. Danh mục Lệnh `curl` kiểm thử từng Endpoint

*(Giả sử server đang chạy tại `http://localhost:5218` hoặc IP Tailscale `http://100.x.y.z:5000`)*

### 1. Kiểm tra trạng thái máy chủ (Health Check)
```bash
curl -i http://localhost:5218/
```
*Kết quả:* HTTP 200 OK kèm thông tin dịch vụ.

### 2. Thử nghiệm xác thực bảo mật (Kiểm tra chặn 401 Unauthorized)
```bash
# Không có header X-API-Key
curl -i -X POST http://localhost:5218/quotes \
  -H "Content-Type: application/json" \
  -d '{"ten_khach_hang":"Test"}'
```
*Kết quả:* `HTTP 401 Unauthorized` - `{"error":"Unauthorized. Header 'X-API-Key' không hợp lệ hoặc bị thiếu."}`.

### 3. Gửi yêu cầu tạo báo giá (POST /quotes)
```bash
curl -i -X POST http://localhost:5218/quotes \
  -H "Content-Type: application/json" \
  -H "X-API-Key: anbinh-chemtech-secret-token" \
  -d '{
    "customer": "Công ty Cổ phần Thực phẩm Á Châu (Asia Foods)",
    "quoteDate": "2026-06-25",
    "quoteNumber": {
      "sequence": "0001",
      "yearMonth": "2606",
      "revision": "01"
    },
    "items": [
      { "name": "Tetrasodium Pyrophosphate (TSPP)", "price_excl_vat": 51560 },
      { "name": "Sodium Tripolyphosphate (STPP)", "price_excl_vat": 51560 },
      { "name": "Sodium Hexametaphosphate (SHMP)", "price_excl_vat": 51560 }
    ]
  }'
```
*Kết quả:* `HTTP 202 Accepted`
```json
{
  "quote_id": "Q-261009-85DC6C",
  "status": "processing",
  "message": "Yêu cầu đã được tiếp nhận và đưa vào hàng đợi xử lý nền."
}
```

### 4. Tra cứu tiến độ xử lý báo giá (GET /quotes/{id})
```bash
curl -i http://localhost:5218/quotes/Q-261009-85DC6C \
  -H "X-API-Key: anbinh-chemtech-secret-token"
```
*Kết quả:* `HTTP 200 OK`
```json
{
  "quote_id": "Q-261009-85DC6C",
  "status": "done",
  "error": null,
  "download_url": "/quotes/Q-261009-85DC6C/file",
  "logs": [
    "Tiếp nhận job vào hàng đợi xử lý.",
    "Đang gọi AI Mapper chuẩn hoá dữ liệu...",
    "AI trích xuất thành công...",
    "Tính toán VAT (tỷ lệ 8.00%)...",
    "Ghi file PowerPoint bằng Open XML SDK...",
    "Xử lý hoàn tất thành công 100%!"
  ]
}
```

### 5. Tải file PowerPoint hoàn chỉnh (GET /quotes/{id}/file)
```bash
curl -O -J http://localhost:5218/quotes/Q-261009-85DC6C/file \
  -H "X-API-Key: anbinh-chemtech-secret-token"
```
*Kết quả:* Tải file `Q-261009-85DC6C.pptx` về máy client. File mở trên Microsoft PowerPoint chuẩn 100%, không báo lỗi Repair, giữ nguyên toàn bộ định dạng font và màu sắc.

---

## 4. Hướng dẫn Thiết lập và Kiểm tra kết nối qua Tailscale

Tailscale là mạng riêng ảo mesh (VPN trên giao thức WireGuard) giúp máy chạy Laravel và máy chạy Executor .NET 8 kết nối trực tiếp với nhau an toàn qua địa chỉ IP tĩnh nội bộ dải `100.x.y.z` mà không cần mở port router (NAT traversal).

### Bước 1: Cài đặt và Đăng nhập Tailscale trên 2 máy
- Cài đặt Tailscale trên cả 2 máy (máy Laravel và máy Executor):
  - **Windows**: Tải installer từ `https://tailscale.com/download/windows`
  - **Linux / Mac**: `curl -fsSL https://tailscale.com/install.sh | sh`
- Đăng nhập chung cùng một tài khoản Tailscale trên cả 2 thiết bị:
  ```bash
  tailscale up
  ```

### Bước 2: Kiểm tra danh sách thiết bị và lấy IP Tailscale
Trên cả 2 máy, chạy lệnh:
```bash
tailscale status
```
*Mẫu kết quả:*
```text
100.80.20.15    laravel-server     linux   -
100.95.40.88    executor-pc        windows -
```
$\rightarrow$ Địa chỉ IP của máy Executor là: `100.95.40.88`.

### Bước 3: Kiểm tra thông mạng (Ping test)
Từ máy Laravel, thực hiện ping tới IP Tailscale của máy Executor:
```bash
tailscale ping 100.95.40.88
# hoặc
ping 100.95.40.88
```
Nếu có phản hồi `pong from executor-pc (100.95.40.88) via ... in 15ms`, mạng riêng đã thông suốt.

### Bước 4: Khởi chạy Executor API lắng nghe trên cổng Tailscale
Trên máy Executor, chạy dịch vụ với URL bind vào mọi card mạng hoặc trực tiếp IP Tailscale:
```bash
# Cách 1: Dùng biến môi trường ASPNETCORE_URLS
set ASPNETCORE_URLS=http://0.0.0.0:5000
dotnet run --project QuoteExecutor

# Hoặc Cách 2: Truyền cờ --urls
dotnet run --project QuoteExecutor --urls "http://0.0.0.0:5000"
```

### Bước 5: Gọi kiểm thử từ máy Laravel tới Executor qua Tailscale
Từ máy Laravel, gửi request qua mạng Tailscale:
```bash
# 1. Health check
curl -i http://100.95.40.88:5000/

# 2. Tạo báo giá
curl -i -X POST http://100.95.40.88:5000/quotes \
  -H "Content-Type: application/json" \
  -H "X-API-Key: anbinh-chemtech-secret-token" \
  -d @sample-request.json

# 3. Tải file về máy Laravel
curl -O -J http://100.95.40.88:5000/quotes/Q-xxxx/file \
  -H "X-API-Key: anbinh-chemtech-secret-token"
```

---

## 5. Hướng dẫn chạy Test và Tự động hóa

```bash
# Chạy bộ Unit Test xUnit (Kiểm thử Open XML, Schema, AiMapper, Math VAT):
dotnet test

# Chế độ quét cấu trúc slide (Dump):
dotnet run --project QuoteFiller -- dump templates/quote-template.pptx

# Chế độ điền file thủ công (CLI):
dotnet run --project QuoteFiller -- fill sample-request.json output/Q-0001.pptx
```

---

## 6. Kiến trúc Phía Laravel Client (Laravel 11)

Mã nguồn được lưu tại thư mục [laravel-quote-client/](file:///d:/NguyenDucTin/AnBinhChemTech/laravel-quote-client/):

1. **Migration bảng `quotations`**:
   - `id`, `customer_code`, `customer_name`, `items` (JSON), `status` (pending/processing/done/failed), `executor_quote_id`, `file_path`, `error`, `timestamps`.
2. **Model `Quotation`**:
   - Cast `items` thành kiểu mảng `array`.
3. **Job `SendQuoteToExecutor`**:
   - Chạy bất đồng bộ qua queue worker.
   - Gọi `Http::withHeaders(['X-API-Key' => ...])->timeout(15)->post(...)`.
   - Timeout 30s, retry tối đa 3 lần với backoff 3s. Nếu thất bại sau 3 lần thì cập nhật `status = 'failed'` kèm log lỗi chi tiết.
4. **Controller `QuotationController`**:
   - `create`: Hiển thị form Blade với 3 sản phẩm mẫu.
   - `store`: Validate, lưu database, dispatch Job và chuyển hướng sang trang chi tiết.
   - `show`: Hiển thị chi tiết báo giá và trạng thái xử lý.
   - `checkStatus`: Endpoint polling mỗi 2.5 giây, tự động tải file PPTX về lưu trữ local khi Executor báo `done`.
   - `download`: Tải file PPTX hoàn chỉnh.
5. **Views (Blade)**:
   - Giao diện thân thiện, hiện đại với Tailwind CSS.
   - JavaScript tự động polling cập nhật Badge trạng thái và hiển thị nút **"Tải File Báo Giá PowerPoint (.pptx)"** ngay khi hoàn tất mà không cần F5/reload trang.

