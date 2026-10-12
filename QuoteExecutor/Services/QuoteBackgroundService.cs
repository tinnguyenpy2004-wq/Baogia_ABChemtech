using System.Text.Json;
using QuoteExecutor.Models;
using QuoteFiller;
using QuoteFiller.Models;
using QuoteFiller.Services;

namespace QuoteExecutor.Services;

public class QuoteBackgroundService : BackgroundService
{
    private readonly IQuoteJobQueue _queue;
    private readonly ILogger<QuoteBackgroundService> _logger;
    private readonly IConfiguration _configuration;
    private readonly AiMapper _aiMapper;
    private readonly QuoteCalculator _calculator;

    public QuoteBackgroundService(
        IQuoteJobQueue queue,
        ILogger<QuoteBackgroundService> logger,
        IConfiguration configuration,
        AiMapper aiMapper,
        QuoteCalculator calculator)
    {
        _queue = queue;
        _logger = logger;
        _configuration = configuration;
        _aiMapper = aiMapper;
        _calculator = calculator;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(">>> Quote Background Processor đã khởi động, sẵn sàng tiếp nhận job...");

        string templatePath = ResolvePath(_configuration["TemplatePath"] ?? "templates/quote-template.pptx");
        string mappingPath = ResolvePath(_configuration["MappingPath"] ?? "mapping.json");

        // Tải trước mapping config
        MappingConfig? mappingConfig = null;
        if (File.Exists(mappingPath))
        {
            try
            {
                mappingConfig = JsonSerializer.Deserialize<MappingConfig>(
                    File.ReadAllText(mappingPath),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể đọc file mapping.json tại {Path}", mappingPath);
            }
        }
        mappingConfig ??= new MappingConfig();

        await foreach (var job in _queue.ReadAllAsync(stoppingToken))
        {
            _logger.LogInformation("[Job {JobId}] Bắt đầu xử lý báo giá...", job.Id);
            job.Logs.Add($"[{DateTime.UtcNow:HH:mm:ss}] Tiếp nhận job vào hàng đợi xử lý.");

            try
            {
                // Bước 1: AI Agent ánh xạ dữ liệu thô từ Laravel sang schema chuẩn
                job.Logs.Add($"[{DateTime.UtcNow:HH:mm:ss}] Đang gọi AI Mapper chuẩn hoá dữ liệu...");
                var aiMapped = await _aiMapper.MapRawRequestAsync(job.RawJson, cancellationToken: stoppingToken);
                job.Logs.Add($"[{DateTime.UtcNow:HH:mm:ss}] AI trích xuất thành công: Khách={aiMapped.CustomerName}, Sản phẩm={aiMapped.Items.Count} mục.");

                // Bước 2: Tính toán VAT và cấu trúc QuoteRequest bằng code xác định
                job.Logs.Add($"[{DateTime.UtcNow:HH:mm:ss}] Tính toán VAT (tỷ lệ {_calculator.VatRate * 100}%)...");
                var quoteRequest = _calculator.BuildQuoteRequest(aiMapped);

                // Bước 3: Xác minh tính đúng đắn toán học
                if (!_calculator.VerifyCalculations(quoteRequest, out var calcErrors))
                {
                    string errText = string.Join("; ", calcErrors);
                    throw new InvalidOperationException($"Lỗi kiểm tra tính toán VAT: {errText}");
                }

                // Bước 4: Tạo thư mục đầu ra và điền dữ liệu vào PPTX bằng Open XML SDK
                string outputDir = Path.GetDirectoryName(job.OutputPath) ?? "outputs";
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                job.Logs.Add($"[{DateTime.UtcNow:HH:mm:ss}] Ghi file PowerPoint bằng Open XML SDK: {job.OutputPath}");
                var fillResult = Filler.FillQuote(templatePath, quoteRequest, mappingConfig, job.OutputPath);

                foreach (var log in fillResult.Logs)
                {
                    job.Logs.Add($"[{DateTime.UtcNow:HH:mm:ss}] [Filler] {log}");
                }

                if (!fillResult.Success)
                {
                    throw new InvalidOperationException("Ghi file PPTX thất bại: " + string.Join("; ", fillResult.Warnings));
                }

                if (fillResult.Warnings.Count > 0)
                {
                    foreach (var warn in fillResult.Warnings)
                    {
                        job.Logs.Add($"[{DateTime.UtcNow:HH:mm:ss}] [CẢNH BÁO] {warn}");
                    }
                }

                job.Status = "done";
                job.CompletedAt = DateTime.UtcNow;
                job.Logs.Add($"[{DateTime.UtcNow:HH:mm:ss}] Xử lý hoàn tất thành công 100%!");
                _logger.LogInformation("[Job {JobId}] Đã hoàn thành xuất file: {Path}", job.Id, job.OutputPath);
            }
            catch (Exception ex)
            {
                job.Status = "failed";
                job.ErrorMessage = ex.Message;
                job.CompletedAt = DateTime.UtcNow;
                job.Logs.Add($"[{DateTime.UtcNow:HH:mm:ss}] [LỖI] {ex.Message}");
                _logger.LogError(ex, "[Job {JobId}] Xử lý thất bại: {Message}", job.Id, ex.Message);
            }
            finally
            {
                _queue.UpdateJob(job);
            }
        }
    }

    private static string ResolvePath(string relativeOrAbsolutePath)
    {
        if (File.Exists(relativeOrAbsolutePath)) return relativeOrAbsolutePath;
        string inAppBase = Path.Combine(AppContext.BaseDirectory, relativeOrAbsolutePath);
        if (File.Exists(inAppBase)) return inAppBase;
        string inCurrent = Path.Combine(Directory.GetCurrentDirectory(), relativeOrAbsolutePath);
        if (File.Exists(inCurrent)) return inCurrent;
        string inParent = Path.Combine(Directory.GetCurrentDirectory(), "..", relativeOrAbsolutePath);
        if (File.Exists(inParent)) return inParent;
        if (File.Exists("01_Bao_Gia_An_Binh_Chemtech.V2.pptx")) return "01_Bao_Gia_An_Binh_Chemtech.V2.pptx";
        return relativeOrAbsolutePath;
    }
}
