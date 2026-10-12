using QuoteFiller;

Console.OutputEncoding = System.Text.Encoding.UTF8;

if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
{
    PrintHelp();
    return 0;
}

string command = args[0].ToLowerInvariant();

if (command == "dump" || command == "--dump")
{
    string targetPath = args.Length > 1 ? args[1] : FindDefaultTemplate();
    Console.WriteLine($"[CHẾ ĐỘ DUMP] Đang đọc cấu trúc file: {targetPath}");
    Dumper.DumpPresentation(targetPath);
    return 0;
}
else if (command == "fill" || command == "--fill")
{
    if (args.Length < 3)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("[LỖI] Thiếu tham số cho lệnh fill.");
        Console.WriteLine("Cú pháp: dotnet run -- fill <request.json> <output.pptx> [template.pptx] [mapping.json]");
        Console.ResetColor();
        return 1;
    }

    string requestJsonPath = args[1];
    string outputPath = args[2];
    string templatePath = args.Length > 3 ? args[3] : FindDefaultTemplate();
    string mappingPath = args.Length > 4 ? args[4] : FindDefaultMapping();

    Console.WriteLine("=== BẮT ĐẦU QUY TRÌNH ĐIỀN BÁO GIÁ (QUOTE FILLER) ===");
    Console.WriteLine($"• Template mẫu   : {templatePath}");
    Console.WriteLine($"• File request   : {requestJsonPath}");
    Console.WriteLine($"• File mapping   : {mappingPath}");
    Console.WriteLine($"• File đầu ra    : {outputPath}");
    Console.WriteLine(new string('-', 60));

    var result = Filler.FillQuote(templatePath, requestJsonPath, mappingPath, outputPath);

    // In log các bước thực hiện
    foreach (var log in result.Logs)
    {
        Console.WriteLine($"[INFO] {log}");
    }

    // In các cảnh báo nếu có
    if (result.Warnings.Count > 0)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n[DANH SÁCH CẢNH BÁO]");
        foreach (var warn in result.Warnings)
        {
            Console.WriteLine($" ! {warn}");
        }
        Console.ResetColor();
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n[XÁC THỰC HOÀN TẤT] File đầu ra sạch 100%, không còn ký tự '[' hoặc ']' nào sót lại.");
        Console.ResetColor();
    }

    if (result.Success)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n--> XUẤT FILE THÀNH CÔNG: {outputPath}");
        Console.ResetColor();
        return 0;
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("\n--> THẤT BẠI: Có lỗi xảy ra trong quá trình xử lý.");
        Console.ResetColor();
        return 1;
    }
}
else
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"[LỖI] Lệnh '{args[0]}' không hợp lệ.");
    Console.ResetColor();
    PrintHelp();
    return 1;
}

static void PrintHelp()
{
    Console.WriteLine(@"
===================================================================
 QuoteFiller - Công cụ tự động tạo PowerPoint Báo Giá (An Bình Chemtech)
===================================================================
Cách dùng:
  dotnet run -- dump [template.pptx]
      Quét và in ra toàn bộ thông tin slide, shape, tên trong Selection Pane,
      số lượng đoạn văn và run trong từng shape.

  dotnet run -- fill <request.json> <output.pptx> [template.pptx] [mapping.json]
      Tạo bản sao từ template, đọc dữ liệu request và mapping để điền thông tin,
      giữ nguyên định dạng run, format tiền tệ và quét cảnh báo sót placeholder.

Ví dụ:
  dotnet run -- dump
  dotnet run -- fill sample-request.json output/Q-0001.pptx
===================================================================
");
}

static string FindDefaultTemplate()
{
    if (File.Exists("templates/quote-template.pptx")) return "templates/quote-template.pptx";
    if (File.Exists("../templates/quote-template.pptx")) return "../templates/quote-template.pptx";
    if (File.Exists("01_Bao_Gia_An_Binh_Chemtech.V2.pptx")) return "01_Bao_Gia_An_Binh_Chemtech.V2.pptx";
    if (File.Exists("../01_Bao_Gia_An_Binh_Chemtech.V2.pptx")) return "../01_Bao_Gia_An_Binh_Chemtech.V2.pptx";
    return "templates/quote-template.pptx";
}

static string FindDefaultMapping()
{
    if (File.Exists("mapping.json")) return "mapping.json";
    if (File.Exists("../mapping.json")) return "../mapping.json";
    return "mapping.json";
}
