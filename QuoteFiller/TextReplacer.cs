using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Drawing;

namespace QuoteFiller;

public static class TextReplacer
{
    // Regex tìm mọi nội dung nằm trong cặp ngoặc vuông, bao gồm cả [], […], [xyz]
    private static readonly Regex BracketRegex = new(@"\[[^\]]*\]", RegexOptions.Compiled);

    /// <summary>
    /// Thay thế các placeholder dạng [...] trong một Shape.
    /// Có thể truyền danh sách replacements theo thứ tự xuất hiện của các cặp ngoặc.
    /// </summary>
    public static int ReplacePlaceholdersInShape(OpenXmlElement shape, params string[] replacements)
    {
        if (replacements == null || replacements.Length == 0) return 0;

        var queue = new Queue<string>(replacements);
        int totalReplaced = 0;

        var paragraphs = shape.Descendants<Paragraph>().ToList();
        foreach (var paragraph in paragraphs)
        {
            if (queue.Count == 0) break;
            totalReplaced += ReplacePlaceholdersInParagraph(paragraph, queue);
        }

        return totalReplaced;
    }

    /// <summary>
    /// Thay thế các placeholder dạng [...] trong một Paragraph, lấy giá trị từ queue theo thứ tự.
    /// Xử lý an toàn trường hợp placeholder bị tách qua nhiều Run.
    /// </summary>
    public static int ReplacePlaceholdersInParagraph(Paragraph paragraph, Queue<string> replacementQueue)
    {
        int replacedCount = 0;

        while (replacementQueue.Count > 0)
        {
            var runs = paragraph.Elements<Run>().Where(r => r.Text != null).ToList();
            if (runs.Count == 0) break;

            // Xây dựng chuỗi toàn bộ paragraph và ánh xạ từng ký tự về Run tương ứng
            var charMap = new List<(int RunIndex, int OffsetInRun)>();
            var fullTextBuilder = new System.Text.StringBuilder();

            for (int rIndex = 0; rIndex < runs.Count; rIndex++)
            {
                var runText = runs[rIndex].Text?.Text ?? string.Empty;
                for (int cIndex = 0; cIndex < runText.Length; cIndex++)
                {
                    charMap.Add((rIndex, cIndex));
                    fullTextBuilder.Append(runText[cIndex]);
                }
            }

            var fullText = fullTextBuilder.ToString();
            var match = BracketRegex.Match(fullText);

            if (!match.Success)
            {
                // Không còn placeholder nào trong paragraph này
                break;
            }

            string nextValue = replacementQueue.Dequeue();
            int matchStart = match.Index;
            int matchLength = match.Length;
            int matchEnd = matchStart + matchLength; // exclusive

            var startPos = charMap[matchStart];
            var endPos = charMap[matchEnd - 1]; // vị trí ký tự ']'

            if (startPos.RunIndex == endPos.RunIndex)
            {
                // Toàn bộ placeholder nằm trong cùng 1 Run
                var run = runs[startPos.RunIndex];
                string t = run.Text!.Text;
                string newRunText = t.Remove(startPos.OffsetInRun, matchLength).Insert(startPos.OffsetInRun, nextValue);
                run.Text.Text = newRunText;
            }
            else
            {
                // Placeholder bị tách qua nhiều Run:
                // Gộp giá trị vào run đầu tiên, xóa phần placeholder ở các run sau, giữ nguyên định dạng của run đầu.
                var firstRun = runs[startPos.RunIndex];
                string firstText = firstRun.Text!.Text;
                firstRun.Text.Text = firstText.Substring(0, startPos.OffsetInRun) + nextValue;

                // Các run ở giữa: xóa sạch
                for (int i = startPos.RunIndex + 1; i < endPos.RunIndex; i++)
                {
                    runs[i].Text!.Text = string.Empty;
                }

                // Run cuối: cắt bỏ phần đầu thuộc placeholder
                var lastRun = runs[endPos.RunIndex];
                string lastText = lastRun.Text!.Text;
                lastRun.Text.Text = lastText.Substring(endPos.OffsetInRun + 1);
            }

            replacedCount++;
        }

        return replacedCount;
    }

    /// <summary>
    /// Thay thế toàn bộ nội dung một Shape bằng một chuỗi (nếu toàn bộ ô là placeholder).
    /// Giữ nguyên định dạng Run đầu tiên.
    /// </summary>
    public static void SetShapeTextPreservingFormat(OpenXmlElement shape, string newText)
    {
        var runs = shape.Descendants<Run>().Where(r => r.Text != null).ToList();
        if (runs.Count == 0) return;

        // Thay placeholder nếu có
        var queue = new Queue<string>(new[] { newText });
        var paragraphs = shape.Descendants<Paragraph>().ToList();
        bool replaced = false;

        foreach (var p in paragraphs)
        {
            if (ReplacePlaceholdersInParagraph(p, queue) > 0)
            {
                replaced = true;
                break;
            }
        }

        if (!replaced)
        {
            // Nếu không có dấu ngoặc vuông, gán trực tiếp vào Run đầu tiên và xóa text các run sau
            runs[0].Text!.Text = newText;
            for (int i = 1; i < runs.Count; i++)
            {
                runs[i].Text!.Text = string.Empty;
            }
        }
    }
}
