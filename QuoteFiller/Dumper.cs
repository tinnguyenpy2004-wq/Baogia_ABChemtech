using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using A = DocumentFormat.OpenXml.Drawing;

namespace QuoteFiller;

public static class Dumper
{
    public static void DumpPresentation(string pptxPath)
    {
        if (!File.Exists(pptxPath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[LỖI] Không tìm thấy file PPTX tại: {pptxPath}");
            Console.ResetColor();
            return;
        }

        Console.WriteLine(new string('=', 90));
        Console.WriteLine($"THÔNG TIN CẤU TRÚC SHAPE TRONG FILE: {Path.GetFileName(pptxPath)}");
        Console.WriteLine(new string('=', 90));

        using var presentationDoc = PresentationDocument.Open(pptxPath, false);
        var presentationPart = presentationDoc.PresentationPart;
        if (presentationPart == null) return;

        var slideIdList = presentationPart.Presentation?.SlideIdList?.Elements<SlideId>();
        if (slideIdList == null) return;

        int slideIndex = 1;
        foreach (var slideId in slideIdList)
        {
            if (string.IsNullOrEmpty(slideId.RelationshipId?.Value)) continue;
            var slidePart = (SlidePart)presentationPart.GetPartById(slideId.RelationshipId.Value);
            var slide = slidePart.Slide;
            if (slide == null) continue;

            Console.WriteLine($"\n>>> SLIDE {slideIndex} <<<");
            Console.WriteLine(new string('-', 90));

            var shapeTree = slide.CommonSlideData?.ShapeTree;
            if (shapeTree == null) continue;

            int shapeOrder = 1;
            foreach (var element in shapeTree.Elements())
            {
                string shapeName = "Không xác định";
                uint shapeId = 0;
                string shapeType = "Khác";
                string fullText = "";
                var paragraphsInfo = new List<(int ParaIndex, int RunCount, string ParaText, List<string> RunsText)>();

                if (element is Shape sp)
                {
                    shapeName = sp.NonVisualShapeProperties?.NonVisualDrawingProperties?.Name?.Value ?? "Chưa đặt tên";
                    shapeId = sp.NonVisualShapeProperties?.NonVisualDrawingProperties?.Id?.Value ?? 0;
                    shapeType = "Text / AutoShape";

                    var textBody = sp.TextBody;
                    if (textBody != null)
                    {
                        int pIdx = 1;
                        foreach (var para in textBody.Elements<A.Paragraph>())
                        {
                            var runs = para.Elements<A.Run>().ToList();
                            var runsText = runs.Select(r => r.Text?.Text ?? "").ToList();
                            string pText = string.Concat(runsText);
                            paragraphsInfo.Add((pIdx++, runs.Count, pText, runsText));
                        }
                        fullText = string.Join(" \n ", paragraphsInfo.Select(p => p.ParaText).Where(t => !string.IsNullOrWhiteSpace(t)));
                    }
                }
                else if (element is Picture pic)
                {
                    shapeName = pic.NonVisualPictureProperties?.NonVisualDrawingProperties?.Name?.Value ?? "Chưa đặt tên";
                    shapeId = pic.NonVisualPictureProperties?.NonVisualDrawingProperties?.Id?.Value ?? 0;
                    shapeType = "Ảnh (Picture)";
                }
                else if (element is GraphicFrame gf)
                {
                    shapeName = gf.NonVisualGraphicFrameProperties?.NonVisualDrawingProperties?.Name?.Value ?? "Chưa đặt tên";
                    shapeId = gf.NonVisualGraphicFrameProperties?.NonVisualDrawingProperties?.Id?.Value ?? 0;
                    var table = gf.Graphic?.GraphicData?.GetFirstChild<A.Table>();
                    if (table != null)
                    {
                        shapeType = "Bảng (Table)";
                        // Lấy text trong bảng
                        var cellsText = new List<string>();
                        foreach (var row in table.Elements<A.TableRow>())
                        {
                            var rowTexts = row.Elements<A.TableCell>().Select(c => c.TextBody?.InnerText ?? "");
                            cellsText.Add(string.Join(" | ", rowTexts));
                        }
                        fullText = string.Join("\n", cellsText);
                    }
                    else
                    {
                        shapeType = "Graphic Frame";
                    }
                }
                else if (element is GroupShape grp)
                {
                    shapeName = grp.NonVisualGroupShapeProperties?.NonVisualDrawingProperties?.Name?.Value ?? "Chưa đặt tên";
                    shapeId = grp.NonVisualGroupShapeProperties?.NonVisualDrawingProperties?.Id?.Value ?? 0;
                    shapeType = "Nhóm (Group)";
                }

                // In thông tin shape
                bool hasBrackets = fullText.Contains('[') || fullText.Contains(']');
                if (hasBrackets)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                }

                Console.WriteLine($"[{shapeOrder:D2}] Tên: \"{shapeName}\" (ID: {shapeId}) | Loại: {shapeType}");
                if (hasBrackets)
                {
                    Console.ResetColor();
                }

                if (!string.IsNullOrWhiteSpace(fullText))
                {
                    // In tóm tắt text
                    string snippet = fullText.Replace("\n", " ").Trim();
                    if (snippet.Length > 80) snippet = snippet.Substring(0, 77) + "...";
                    Console.WriteLine($"     Nội dung: {snippet}");

                    // Chi tiết từng đoạn văn và run nếu có ngoặc vuông hoặc có nhiều run
                    foreach (var p in paragraphsInfo)
                    {
                        if (p.ParaText.Contains('[') || p.RunCount > 1)
                        {
                            Console.WriteLine($"     └ Đoạn #{p.ParaIndex}: (Số run = {p.RunCount})");
                            for (int r = 0; r < p.RunsText.Count; r++)
                            {
                                string rText = p.RunsText[r];
                                bool rHasBracket = rText.Contains('[') || rText.Contains(']');
                                string bracketFlag = rHasBracket ? " <-- [CHỨA NGOẶC]" : "";
                                Console.WriteLine($"        • Run {r + 1}: \"{rText}\"{bracketFlag}");
                            }
                        }
                    }
                }

                shapeOrder++;
            }

            slideIndex++;
        }

        Console.WriteLine(new string('=', 90));
    }
}
