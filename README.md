# Bao Gia Automation

Prototype tự động hóa quy trình tạo báo giá cho An Bình Chemtech. Mục tiêu của prototype là chứng minh luồng nghiệp vụ chính có thể chạy local, dễ kiểm tra và có ranh giới rõ giữa phần code xác định với phần AI tùy chọn.

## 1. Tổng quan giải pháp

### Kiến trúc

```text
BaoGiaAutomation.sln
|-- SOLUTION_PLAN.md              # Thiết kế kiến trúc và quyết định kỹ thuật
|-- src
|   |-- BaoGia.Api                # REST API, static frontend, job store
|   |-- BaoGia.Domain             # QuoteJob và quy tắc trạng thái
|   `-- BaoGia.Worker             # Điểm mở rộng cho worker Mac mini độc lập
`-- tests
    `-- BaoGia.Tests              # Unit test cho domain
```

Trong bản prototype, `QuoteProcessingWorker` được host ngay trong `BaoGia.Api` để người chấm có thể chạy bằng một lệnh, không cần cài message broker hoặc mở cổng vào Mac mini. Worker nền nhận job trong memory, mô phỏng thời gian xử lý và sinh file báo giá HTML.

### Luồng xử lý

1. Người dùng mở trang Customer Profile và nhập sản phẩm, số lượng, đơn giá, điều khoản thanh toán/giao hàng.
2. Frontend gửi `POST /api/quotes` kèm `IdempotencyKey`.
3. API validate request, tính tổng tiền từ dữ liệu server và tạo job ở trạng thái `PENDING`.
4. Background worker claim job, chuyển sang `PROCESSING`, sau đó render file báo giá bằng code xác định.
5. Nếu tạo file thành công, job chuyển sang `COMPLETED`; nếu có exception, job chuyển sang `FAILED` kèm lỗi.
6. Dashboard tự đọc danh sách job và hiển thị nút tải file khi có `downloadUrl`.

Các trạng thái API trả về là `PENDING`, `PROCESSING`, `COMPLETED` và `FAILED`.

## 2. Cài đặt và chạy local

### Yêu cầu

- Windows, macOS hoặc Linux.
- .NET SDK 8.0 trở lên.
- Không cần database, Node.js, Docker hay Codex CLI cho bản demo.

### Chạy nhanh

Từ thư mục chứa `BaoGiaAutomation.sln`:

```powershell
dotnet run --project src/BaoGia.Api --urls http://localhost:5078
```

Mở các địa chỉ sau:

- Demo UI: `http://localhost:5078/`
- Swagger: `http://localhost:5078/swagger`

### Kiểm tra build và test

```powershell
dotnet build BaoGiaAutomation.sln
dotnet test BaoGiaAutomation.sln
```

Sau khi bấm **Gửi yêu cầu báo giá**, job ban đầu có thể hiển thị `PENDING` hoặc `PROCESSING`, sau khoảng một giây sẽ chuyển sang `COMPLETED`. File sinh ra là HTML và có thể mở trực tiếp trong trình duyệt.

## 3. Phạm vi hoàn thành và phần giả lập

### Đã hoàn thành

- Customer Profile với dữ liệu khách hàng giả lập.
- Form nhập sản phẩm, số lượng, đơn giá và điều khoản.
- REST API tạo và truy vấn quote job.
- Validation cơ bản: trường bắt buộc, giới hạn độ dài, số lượng dương, đơn giá hợp lệ.
- Tính lại tổng tiền trên server, không tin tổng tiền từ client.
- Idempotency cơ bản bằng `IdempotencyKey` trong memory.
- Trạng thái `PENDING -> PROCESSING -> COMPLETED/FAILED`.
- Background worker xử lý bất đồng bộ.
- Render file báo giá HTML và endpoint download.
- Swagger/OpenAPI và unit test cho vòng đời `QuoteJob`.

### Phần đang mock hoặc giới hạn trong prototype

| Thành phần | Trạng thái hiện tại | Hướng thay thế khi triển khai thật |
|---|---|---|
| CRM/khách hàng | Dữ liệu hard-code trong frontend | Kết nối CRM hoặc database nội bộ, có phân quyền |
| Database | `ConcurrentDictionary` trong memory | SQLite cho pilot hoặc PostgreSQL/SQL Server cho production |
| Mac mini worker | Worker background chạy trong API | Tách thành process/service trên Mac mini, pull job qua HTTPS |
| Template | HTML tạo trực tiếp bằng code | Dùng template DOCX/XLSX/PDF versioned và renderer riêng |
| Codex CLI/LLM | Chưa gọi trong runtime | Đặt sau interface `ICodexClient`, chỉ xử lý ghi chú tự do hoặc đề xuất mapping |
| File storage | Thư mục `generated` của API | Object storage như S3/MinIO/Azure Blob |
| Authentication | Chưa bật cho demo local | OIDC/JWT, role-based authorization và audit log |

Việc không dùng AI để điền các con số là có chủ ý: phép tính tiền, mapping template và kiểm tra file cần kết quả lặp lại được. AI chỉ nên hỗ trợ phần ngôn ngữ hoặc đề xuất, không tự quyết định số tiền và trạng thái nghiệp vụ.

## 4. Minh bạch việc sử dụng AI

Trong quá trình hoàn thiện prototype, tôi sử dụng **GitHub Copilot trong VS Code** để:

- phân rã yêu cầu thành API, domain, worker và frontend;
- đề xuất skeleton .NET solution và các DTO/controller/service;
- hỗ trợ viết giao diện demo và mã render báo giá;
- đọc lỗi compile/runtime, xác định lỗi interpolated raw string và lỗi validation phụ thuộc culture;
- đề xuất test smoke cho luồng tạo job, worker xử lý và download file.

Các quyết định cuối cùng, phạm vi mock, cách xử lý tiền tệ và kết quả kiểm thử được tôi kiểm tra lại bằng code review cục bộ, `dotnet build`, `dotnet test` và request HTTP thực tế.

AI không được gọi trong runtime của prototype. Không có secret, dữ liệu khách hàng thật hoặc thông tin nhạy cảm nào được đưa vào prompt. Nếu bổ sung Codex CLI trong tương lai, output cần có schema, timeout, sandbox và fallback về dữ liệu gốc.

## 5. Giả định và giới hạn

- Prototype chỉ phục vụ một người dùng và một tiến trình API tại một thời điểm.
- Dữ liệu mất khi ứng dụng restart vì job store đang ở memory.
- File HTML được dùng thay cho DOCX/PDF để giảm dependency và giúp demo chạy ngay.
- Chưa mô phỏng đầy đủ retry, lease, queue persistence và concurrent worker claim như thiết kế trong `SOLUTION_PLAN.md`.
- Các giá trị tiền đang dùng `decimal` trong code; production cần bổ sung currency, VAT, rounding rule và test nghiệp vụ với kế toán.

## 6. Định hướng Production

### Reliability và scale

- Thay `ConcurrentDictionary` bằng PostgreSQL/SQL Server hoặc SQLite có migration.
- Dùng RabbitMQ, Azure Service Bus hoặc Redis-backed queue để tách API khỏi worker. Với hệ sinh thái Node.js có thể cân nhắc BullMQ; với solution hiện tại, RabbitMQ hoặc Azure Service Bus phù hợp hơn.
- Thêm retry có exponential backoff, giới hạn số lần thử, dead-letter queue, lease/visibility timeout và idempotent completion.
- Dùng health check, structured logging, correlation ID, metrics và cảnh báo khi queue tăng hoặc worker mất heartbeat.
- Tách renderer thành worker container/service và giới hạn CPU, memory, filesystem permission.

### Security và dữ liệu

- Bật OIDC/JWT hoặc ASP.NET Core Identity; phân quyền theo vai trò và theo khách hàng.
- Dùng HTTPS, secret manager, API key riêng cho worker và cơ chế rotate key.
- Lưu file trong S3/MinIO/Azure Blob với private bucket, signed URL có thời hạn và checksum.
- Chống path traversal, formula injection, upload độc hại và prompt injection trong ghi chú tự do.
- Thêm audit log, retention policy, backup/restore và mã hóa dữ liệu nhạy cảm khi cần.

### Vận hành và phát hành

- Dockerize API và worker; cấu hình bằng environment variables, health probes và graceful shutdown.
- Thiết lập CI/CD gồm restore, build, unit/integration test, security scan và migration kiểm soát.
- Version hóa template báo giá, có test fixture để xác nhận placeholder, tổng tiền và định dạng đầu ra.
- Bổ sung integration test cho idempotency, retry, worker crash, file bị khóa và mất kết nối.
- Chỉ đưa Codex CLI/LLM vào các bước có thể fallback an toàn; mọi output AI phải được validate trước khi sử dụng.

## 7. File báo giá mẫu xuất từ prototype

- File HTML mẫu: [samples/sample-quote.html](samples/sample-quote.html)

## Tài liệu liên quan

- [SOLUTION_PLAN.md](SOLUTION_PLAN.md): thiết kế kiến trúc chi tiết, data flow Mermaid và rủi ro vận hành.
