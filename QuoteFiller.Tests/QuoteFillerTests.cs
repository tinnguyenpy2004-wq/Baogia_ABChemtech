using System.Globalization;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Validation;
using QuoteFiller;
using QuoteFiller.Models;
using Xunit;

namespace QuoteFiller.Tests;

public class QuoteFillerTests
{
    private readonly string _templatePath;
    private readonly string _requestPath;
    private readonly string _mappingPath;
    private readonly string _outputTestPath;

    public QuoteFillerTests()
    {
        // Định vị các file mẫu và file test độc lập
        string baseDir = AppContext.BaseDirectory;
        string projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));

        _templatePath = Path.Combine(projectRoot, "templates", "quote-template.pptx");
        if (!File.Exists(_templatePath))
        {
            _templatePath = Path.Combine(projectRoot, "01_Bao_Gia_An_Binh_Chemtech.V2.pptx");
        }

        _requestPath = Path.Combine(projectRoot, "sample-request.json");
        _mappingPath = Path.Combine(projectRoot, "mapping.json");

        string testOutputDir = Path.Combine(projectRoot, "output", "test");
        Directory.CreateDirectory(testOutputDir);
        _outputTestPath = Path.Combine(testOutputDir, $"test-quote-{Guid.NewGuid():N}.pptx");
    }

    [Fact]
    public void FillQuote_ShouldRemoveAllBracketsAndPlaceholders()
    {
        // Act
        var result = Filler.FillQuote(_templatePath, _requestPath, _mappingPath, _outputTestPath);

        // Assert
        Assert.True(result.Success, "Quá trình điền báo giá phải trả về Success = true");
        Assert.Empty(result.Warnings); // Không được có bất kỳ cảnh báo sót placeholder nào

        // Kiểm tra trực tiếp toàn bộ text trong file
        using var doc = PresentationDocument.Open(_outputTestPath, false);
        var slide = doc.PresentationPart?.SlideParts.First().Slide;
        Assert.NotNull(slide);

        string allText = slide.InnerText;
        Assert.DoesNotContain("[", allText);
        Assert.DoesNotContain("]", allText);
    }

    [Fact]
    public void FillQuote_ShouldHaveCorrectFilledValues()
    {
        // Act
        var result = Filler.FillQuote(_templatePath, _requestPath, _mappingPath, _outputTestPath);
        Assert.True(result.Success);

        // Assert
        using var doc = PresentationDocument.Open(_outputTestPath, false);
        var slide = doc.PresentationPart?.SlideParts.First().Slide;
        Assert.NotNull(slide);

        var shapes = slide.Descendants<Shape>().ToList();

        // 1. Kiểm tra Số báo giá
        var quoteNoSp = shapes.FirstOrDefault(s => s.InnerText.Contains("ABC-0001/QT/2606/01"));
        Assert.NotNull(quoteNoSp);

        // 2. Kiểm tra Khách hàng
        var customerSp = shapes.FirstOrDefault(s => s.InnerText.Contains("Công ty Cổ phần Thực phẩm Á Châu (Asia Foods)"));
        Assert.NotNull(customerSp);
        Assert.Contains("Quý khách hàng / Quý đối tác:", customerSp.InnerText);

        // 3. Kiểm tra Ngày tháng
        var dateSp = shapes.FirstOrDefault(s => s.InnerText.Contains("Ngày 25 tháng 06 năm 2026"));
        Assert.NotNull(dateSp);

        // 4. Kiểm tra Định dạng tiền tệ có dấu phẩy (51,560 và 55,685)
        var priceWithoutVatSp = shapes.FirstOrDefault(s => s.InnerText.Trim() == "51,560");
        Assert.NotNull(priceWithoutVatSp);

        var priceWithVatSp = shapes.FirstOrDefault(s => s.InnerText.Trim() == "55,685");
        Assert.NotNull(priceWithVatSp);
    }

    [Fact]
    public void OutputPptx_ShouldBeValidAndOpenWithoutError()
    {
        // Act
        var result = Filler.FillQuote(_templatePath, _requestPath, _mappingPath, _outputTestPath);
        Assert.True(result.Success);

        // Assert: Mở và validate file PPTX bằng OpenXmlValidator
        using var doc = PresentationDocument.Open(_outputTestPath, false);
        Assert.NotNull(doc.PresentationPart);
        Assert.NotEmpty(doc.PresentationPart.SlideParts);

        var validator = new OpenXmlValidator();
        var errors = validator.Validate(doc)
            .Where(e => e.ErrorType == ValidationErrorType.Schema)
            .ToList();

        // Không có lỗi cấu trúc XML nghiêm trọng
        Assert.Empty(errors);
    }
}
