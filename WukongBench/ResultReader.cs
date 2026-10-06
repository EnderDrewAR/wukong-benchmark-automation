using System.Globalization;
using System.Text.RegularExpressions;
using Tesseract;

namespace WukongBench;

internal sealed record BenchmarkResult(int AverageFps, int MinimumFps, int MaximumFps, int FifthPercentileFps,
    Dictionary<string, string> ObservedFields);

internal sealed class ResultReader : IDisposable
{
    private readonly TesseractEngine digits = new(Path.Combine(AppContext.BaseDirectory, "tessdata"), "eng");
    public ResultReader() => digits.SetVariable("tessedit_char_whitelist", "0123456789");

    public BenchmarkResult Read(string screenshot, ScreenText text, string directory)
    {
        if (!IsResult(text)) throw new InvalidOperationException("Это не экран результатов бенчмарка.");
        using var source = new Bitmap(screenshot);
        int Number(string name, params string[] labels)
        {
            var label = text.Lines.FirstOrDefault(l => labels.Any(s => l.Text.Contains(s, StringComparison.OrdinalIgnoreCase)))
                ?? throw new InvalidOperationException("Не найдена подпись " + name);
            var rect = Rectangle.Intersect(new Rectangle(0, 0, source.Width, source.Height),
                new Rectangle((int)label.X, (int)(label.Y + label.Height + 3),
                    (int)(source.Width * .115), (int)(source.Height * (name == "average" ? .069 : .045))));
            using var crop = source.Clone(rect, source.PixelFormat);
            for (var y = 0; y < crop.Height; y++)
                for (var x = 0; x < crop.Width; x++)
                {
                    var c = crop.GetPixel(x, y);
                    crop.SetPixel(x, y, c.G > c.R * 1.2 && c.G > 65 ? Color.Black : Color.White);
                }
            var cropPath = Path.Combine(directory, name + "-digits.png");
            crop.Save(cropPath);
            using var pix = Pix.LoadFromFile(cropPath);
            using var page = digits.Process(pix, PageSegMode.SingleLine);
            var value = page.GetText().Trim();
            if (!Regex.IsMatch(value, @"^\d{1,4}$") || !int.TryParse(value, CultureInfo.InvariantCulture, out var n) || n <= 0)
                throw new InvalidOperationException($"Не удалось распознать {name}: '{value}'. См. {cropPath}");
            return n;
        }
        var average = Number("average", "В среднем", "Average");
        var minimum = Number("minimum", "Минимум", "Minimum");
        var maximum = Number("maximum", "Максимум", "Maximum");
        var fifth = Number("percentile5", "перцентиль", "percentile", "percent");
        if (minimum > average || average > maximum || fifth < minimum || fifth > maximum)
            throw new InvalidOperationException("Распознанные FPS противоречат друг другу.");
        var fields = new Dictionary<string, string>();
        foreach (var label in new[] { "Процессор", "Видеокарта", "Видеопамять", "Оперативная память", "Версия игры", "Драйвер видеокарты", "Разрешение экрана", "Генерация кадров", "Полная трассировка лучей", "Степень", "Набор настроек", "CPU", "GPU", "Memory", "Resolution", "Frame Generation", "Full Ray Tracing" })
        {
            var line = text.Lines.FirstOrDefault(l => l.Text.StartsWith(label, StringComparison.OrdinalIgnoreCase));
            if (line is null) continue;
            var below = text.Lines.Where(l => Math.Abs(l.X - line.X) < source.Width * .03 && l.Y > line.Y + line.Height * .8)
                .OrderBy(l => l.Y).FirstOrDefault();
            if (below is not null) fields[label] = below.Text;
        }
        return new BenchmarkResult(average, minimum, maximum, fifth, fields);
    }

    public static bool IsResult(ScreenText text)
    {
        var compact = Regex.Replace(text.Text, @"\s+", "");
        return (compact.Contains("Результатытеста", StringComparison.OrdinalIgnoreCase)
                && compact.Contains("Всреднем", StringComparison.OrdinalIgnoreCase)
                && compact.Contains("перцентил", StringComparison.OrdinalIgnoreCase))
            || (compact.Contains("BenchmarkResults", StringComparison.OrdinalIgnoreCase)
                && compact.Contains("Average", StringComparison.OrdinalIgnoreCase)
                && compact.Contains("percent", StringComparison.OrdinalIgnoreCase));
    }
    public void Dispose() => digits.Dispose();
}
