using System.Globalization;
using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using QuoteFiller.Models;
using A = DocumentFormat.OpenXml.Drawing;

namespace QuoteFiller;

public class FillerResult
{
    public bool Success { get; set; }
    public string OutputPath { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = new();
    public List<string> Logs { get; set; } = new();
}

public static class Filler
{
    private static readonly CultureInfo UsCulture = new("en-US");

    public static FillerResult FillQuote(string templatePath, string requestJsonPath, string mappingJsonPath, string outputPath)
    {
        var result = new FillerResult { OutputPath = outputPath };

        // 1. Kiểm tra file đầu vào
        if (!File.Exists(templatePath))
        {
            result.Warnings.Add($"Không tìm thấy file template: {templatePath}");
            return result;
        }

        if (!File.Exists(requestJsonPath))
        {
            result.Warnings.Add($"Không tìm thấy file request: {requestJsonPath}");
            return result;
        }

        if (!File.Exists(mappingJsonPath))
        {
            result.Warnings.Add($"Không tìm thấy file mapping: {mappingJsonPath}");
            return result;
        }

        // 2. Đọc file JSON
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        QuoteRequest? request = JsonSerializer.Deserialize<QuoteRequest>(File.ReadAllText(requestJsonPath), jsonOptions);
        MappingConfig? mapping = JsonSerializer.Deserialize<MappingConfig>(File.ReadAllText(mappingJsonPath), jsonOptions);

        if (request == null || mapping == null)
        {
            result.Warnings.Add("Không thể giải mã dữ liệu JSON từ request hoặc mapping.");
            return result;
        }

        return FillQuote(templatePath, request, mapping, outputPath);
    }

    public static FillerResult FillQuote(string templatePath, QuoteRequest request, MappingConfig mapping, string outputPath)
    {
        var result = new FillerResult { OutputPath = outputPath };

        if (!File.Exists(templatePath))
        {
            result.Warnings.Add($"Không tìm thấy file template: {templatePath}");
            return result;
        }

        // 3. Tạo thư mục output và sao chép template ra output (KHÔNG đụng file gốc)
        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        File.Copy(templatePath, outputPath, true);
        result.Logs.Add($"Đã tạo bản sao template tại: {outputPath}");

        // 4. Mở file PPTX output bằng Open XML SDK
        using (var presentationDoc = PresentationDocument.Open(outputPath, true))
        {
            var presentationPart = presentationDoc.PresentationPart;
            if (presentationPart == null)
            {
                result.Warnings.Add("File PPTX không chứa PresentationPart hợp lệ.");
                return result;
            }

            var slidePart = presentationPart.SlideParts.FirstOrDefault();
            if (slidePart == null)
            {
                result.Warnings.Add("File PPTX không chứa slide nào.");
                return result;
            }

            var slide = slidePart.Slide;
            if (slide == null)
            {
                result.Warnings.Add("Slide rỗng.");
                return result;
            }

            var shapeTree = slide.CommonSlideData?.ShapeTree;
            if (shapeTree == null)
            {
                result.Warnings.Add("Slide không chứa ShapeTree.");
                return result;
            }

            // A. Điền số báo giá
            var quoteNoShape = FindShape(slide, mapping.QuoteNumberShape, mapping);
            if (quoteNoShape != null)
            {
                var qn = request.QuoteNumber ?? new QuoteNumberInfo();
                int count = TextReplacer.ReplacePlaceholdersInShape(quoteNoShape, qn.Sequence, qn.YearMonth, qn.Revision);
                result.Logs.Add($"Đã điền Số báo giá (thay thế {count} placeholder): ABC-{qn.Sequence}/QT/{qn.YearMonth}/{qn.Revision}");
            }
            else
            {
                result.Warnings.Add($"Không tìm thấy shape số báo giá: {mapping.QuoteNumberShape}");
            }

            // B. Điền tên khách hàng
            var customerShape = FindShape(slide, mapping.CustomerShape, mapping);
            if (customerShape != null)
            {
                string customerName = request.Customer ?? "Quý khách hàng";
                int count = TextReplacer.ReplacePlaceholdersInShape(customerShape, customerName);
                result.Logs.Add($"Đã điền Khách hàng: {customerName} (thay thế {count} placeholder)");
            }
            else
            {
                result.Warnings.Add($"Không tìm thấy shape khách hàng: {mapping.CustomerShape}");
            }

            // C. Điền ngày tháng báo giá
            var dateShape = FindShape(slide, mapping.QuoteDateShape, mapping);
            if (dateShape != null)
            {
                DateTime dt = DateTime.Now;
                if (!string.IsNullOrWhiteSpace(request.QuoteDate) && DateTime.TryParse(request.QuoteDate, out var parsedDate))
                {
                    dt = parsedDate;
                }
                string dayStr = dt.Day.ToString("D2");
                string monthStr = dt.Month.ToString("D2");
                int count = TextReplacer.ReplacePlaceholdersInShape(dateShape, dayStr, monthStr);
                result.Logs.Add($"Đã điền Ngày tháng báo giá: Ngày {dayStr} tháng {monthStr} (thay thế {count} placeholder)");
            }
            else
            {
                result.Warnings.Add($"Không tìm thấy shape ngày tháng: {mapping.QuoteDateShape}");
            }

            // D. Điền danh sách sản phẩm (Bảng hàng hóa)
            FillQuoteItems(slide, shapeTree, request.Items, mapping, result);

            // Lưu thay đổi trên slide
            slide?.Save();
        }

        // 5. Quét toàn bộ file sau khi điền để tìm placeholder sót
        ScanRemainingBrackets(outputPath, result);

        result.Success = true;
        return result;
    }

    /// <summary>
    /// Điền thông tin các dòng sản phẩm, nhân bản hoặc xóa dòng nếu số lượng khác 3.
    /// </summary>
    private static void FillQuoteItems(Slide slide, ShapeTree shapeTree, List<QuoteItem> items, MappingConfig mapping, FillerResult result)
    {
        int templateRowCount = mapping.Items.Count;
        int targetRowCount = items.Count;

        // Điền các dòng sẵn có
        int rowsToFill = Math.Min(templateRowCount, targetRowCount);
        for (int i = 0; i < rowsToFill; i++)
        {
            var item = items[i];
            var itemMap = mapping.Items[i];

            // Tên hàng
            var nameSp = FindShape(slide, itemMap.NameShape, mapping);
            if (nameSp != null)
            {
                TextReplacer.ReplacePlaceholdersInShape(nameSp, item.ProductName);
            }

            // Đơn giá chưa VAT (định dạng số tiền dấu phẩy N0, vd 51,560)
            var priceWithoutVatSp = FindShape(slide, itemMap.PriceWithoutVatShape, mapping);
            if (priceWithoutVatSp != null)
            {
                string formattedPrice = item.PriceWithoutVat.ToString("N0", UsCulture);
                TextReplacer.ReplacePlaceholdersInShape(priceWithoutVatSp, formattedPrice);
            }

            // Đơn giá có VAT
            var priceWithVatSp = FindShape(slide, itemMap.PriceWithVatShape, mapping);
            if (priceWithVatSp != null)
            {
                string formattedPrice = item.CalculatedPriceWithVat.ToString("N0", UsCulture);
                TextReplacer.ReplacePlaceholdersInShape(priceWithVatSp, formattedPrice);
            }

            result.Logs.Add($"Dòng {i + 1}: {item.ProductName} | Chưa VAT: {item.PriceWithoutVat:N0} | Có VAT: {item.CalculatedPriceWithVat:N0}");
        }

        // Trường hợp số sản phẩm ít hơn 3: Xóa hoặc ẩn các dòng thừa
        if (targetRowCount < templateRowCount)
        {
            for (int i = targetRowCount; i < templateRowCount; i++)
            {
                var itemMap = mapping.Items[i];
                RemoveOrClearShape(slide, itemMap.NameShape, mapping);
                RemoveOrClearShape(slide, itemMap.PriceWithoutVatShape, mapping);
                RemoveOrClearShape(slide, itemMap.PriceWithVatShape, mapping);

                // Đồng thời dọn ô STT và Quy cách tương ứng của dòng đó
                int rowIndex = i + 1;
                RemoveOrClearShape(slide, $"txt_stt_{rowIndex}", mapping);
                RemoveOrClearShape(slide, $"txt_quy_cach_{rowIndex}", mapping);
                // Kiểm tra alias theo Id: STT dòng 2=31, dòng 3=35; Quy cách dòng 2=50, dòng 3=51
                if (rowIndex == 2)
                {
                    RemoveShapeById(slide, 31); // STT 2
                    RemoveShapeById(slide, 50); // Quy cách 2
                }
                else if (rowIndex == 3)
                {
                    RemoveShapeById(slide, 35); // STT 3
                    RemoveShapeById(slide, 51); // Quy cách 3
                }

                result.Logs.Add($"Đã dọn dẹp hàng mẫu thừa thứ {rowIndex}.");
            }
        }
        // Trường hợp số sản phẩm nhiều hơn 3: Nhân bản shape dòng cuối
        else if (targetRowCount > templateRowCount)
        {
            var lastMap = mapping.Items[templateRowCount - 1];
            var baseNameSp = FindShape(slide, lastMap.NameShape, mapping) as Shape;
            var basePriceWithoutVatSp = FindShape(slide, lastMap.PriceWithoutVatShape, mapping) as Shape;
            var basePriceWithVatSp = FindShape(slide, lastMap.PriceWithVatShape, mapping) as Shape;

            // Tính khoảng cách deltaY dựa vào dòng 2 và dòng 3 nếu có
            long deltaY = 320000; // ~0.35 inch tính bằng EMUs (giá trị mặc định chuẩn của slide)
            if (templateRowCount >= 2)
            {
                var prevMap = mapping.Items[templateRowCount - 2];
                var prevNameSp = FindShape(slide, prevMap.NameShape, mapping) as Shape;
                if (baseNameSp?.ShapeProperties?.Transform2D?.Offset?.Y != null &&
                    prevNameSp?.ShapeProperties?.Transform2D?.Offset?.Y != null)
                {
                    deltaY = baseNameSp.ShapeProperties.Transform2D.Offset.Y.Value -
                             prevNameSp.ShapeProperties.Transform2D.Offset.Y.Value;
                }
            }

            uint maxId = slide.Descendants<NonVisualDrawingProperties>()
                              .Select(p => p.Id?.Value ?? 0)
                              .DefaultIfEmpty(100u)
                              .Max();

            for (int i = templateRowCount; i < targetRowCount; i++)
            {
                var item = items[i];
                int multiplier = i - templateRowCount + 1;
                long rowOffset = deltaY * multiplier;

                // Nhân bản Tên hàng
                if (baseNameSp != null)
                {
                    var clonedName = (Shape)baseNameSp.CloneNode(true);
                    UpdateClonedShape(clonedName, ++maxId, $"txt_ten_hang_{i + 1}", rowOffset);
                    TextReplacer.SetShapeTextPreservingFormat(clonedName, item.ProductName);
                    shapeTree.AppendChild(clonedName);
                }

                // Nhân bản Giá chưa VAT
                if (basePriceWithoutVatSp != null)
                {
                    var clonedP1 = (Shape)basePriceWithoutVatSp.CloneNode(true);
                    UpdateClonedShape(clonedP1, ++maxId, $"txt_don_gia_chua_vat_{i + 1}", rowOffset);
                    TextReplacer.SetShapeTextPreservingFormat(clonedP1, item.PriceWithoutVat.ToString("N0", UsCulture));
                    shapeTree.AppendChild(clonedP1);
                }

                // Nhân bản Giá có VAT
                if (basePriceWithVatSp != null)
                {
                    var clonedP2 = (Shape)basePriceWithVatSp.CloneNode(true);
                    UpdateClonedShape(clonedP2, ++maxId, $"txt_don_gia_da_bao_gom_vat_{i + 1}", rowOffset);
                    TextReplacer.SetShapeTextPreservingFormat(clonedP2, item.CalculatedPriceWithVat.ToString("N0", UsCulture));
                    shapeTree.AppendChild(clonedP2);
                }

                result.Logs.Add($"Đã nhân bản thêm dòng {i + 1}: {item.ProductName}");
            }
        }
    }

    private static void UpdateClonedShape(Shape shape, uint newId, string newName, long yOffsetDelta)
    {
        if (shape.NonVisualShapeProperties?.NonVisualDrawingProperties != null)
        {
            shape.NonVisualShapeProperties.NonVisualDrawingProperties.Id = newId;
            shape.NonVisualShapeProperties.NonVisualDrawingProperties.Name = newName;
        }

        var offset = shape.ShapeProperties?.Transform2D?.Offset;
        if (offset?.Y != null)
        {
            offset.Y = offset.Y.Value + yOffsetDelta;
        }
    }

    private static void RemoveOrClearShape(Slide slide, string shapeName, MappingConfig mapping)
    {
        var sp = FindShape(slide, shapeName, mapping);
        if (sp != null)
        {
            sp.Remove();
        }
    }

    private static void RemoveShapeById(Slide slide, uint id)
    {
        var sp = slide.Descendants<Shape>().FirstOrDefault(s => s.NonVisualShapeProperties?.NonVisualDrawingProperties?.Id?.Value == id);
        sp?.Remove();
    }

    /// <summary>
    /// Tìm Shape theo tên chính thức hoặc danh sách Alias trong cấu hình mapping.
    /// </summary>
    public static OpenXmlElement? FindShape(Slide slide, string shapeName, MappingConfig mapping)
    {
        var shapes = slide.Descendants<Shape>().Cast<OpenXmlElement>()
            .Concat(slide.Descendants<GraphicFrame>())
            .ToList();

        // 1. Tìm chính xác theo tên
        var found = shapes.FirstOrDefault(s => GetElementName(s) == shapeName);
        if (found != null) return found;

        // 2. Tìm theo danh sách alias từ mapping.json
        if (mapping.ShapeNameAliases.TryGetValue(shapeName, out var aliases))
        {
            foreach (var alias in aliases)
            {
                found = shapes.FirstOrDefault(s => GetElementName(s) == alias);
                if (found != null) return found;

                // Thử tìm theo ID nếu alias có định dạng "Google Shape;ID;p1"
                if (alias.StartsWith("Google Shape;") && uint.TryParse(alias.Split(';')[1], out uint shapeId))
                {
                    found = shapes.FirstOrDefault(s => GetElementId(s) == shapeId);
                    if (found != null) return found;
                }
            }
        }

        return null;
    }

    private static string GetElementName(OpenXmlElement element)
    {
        if (element is Shape sp)
            return sp.NonVisualShapeProperties?.NonVisualDrawingProperties?.Name?.Value ?? "";
        if (element is GraphicFrame gf)
            return gf.NonVisualGraphicFrameProperties?.NonVisualDrawingProperties?.Name?.Value ?? "";
        if (element is Picture pic)
            return pic.NonVisualPictureProperties?.NonVisualDrawingProperties?.Name?.Value ?? "";
        return "";
    }

    private static uint GetElementId(OpenXmlElement element)
    {
        if (element is Shape sp)
            return sp.NonVisualShapeProperties?.NonVisualDrawingProperties?.Id?.Value ?? 0;
        if (element is GraphicFrame gf)
            return gf.NonVisualGraphicFrameProperties?.NonVisualDrawingProperties?.Id?.Value ?? 0;
        if (element is Picture pic)
            return pic.NonVisualPictureProperties?.NonVisualDrawingProperties?.Id?.Value ?? 0;
        return 0;
    }

    /// <summary>
    /// Quét toàn file PPTX đầu ra: nếu còn ký tự '[' hoặc ']' thì ghi lại cảnh báo kèm tên shape.
    /// </summary>
    public static void ScanRemainingBrackets(string pptxPath, FillerResult result)
    {
        using var doc = PresentationDocument.Open(pptxPath, false);
        var part = doc.PresentationPart;
        if (part == null) return;

        foreach (var slidePart in part.SlideParts)
        {
            var shapeTree = slidePart.Slide?.CommonSlideData?.ShapeTree;
            if (shapeTree == null) continue;

            foreach (var element in shapeTree.Elements())
            {
                string name = GetElementName(element);
                string text = element.InnerText;

                if (text.Contains('[') || text.Contains(']'))
                {
                    string warn = $"[CẢNH BÁO SÓT PLACEHOLDER] Shape \"{name}\" vẫn còn chứa ký tự '[' hoặc ']': \"{text}\"";
                    result.Warnings.Add(warn);
                }
            }
        }
    }
}
