using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;

namespace WukongBench;

internal sealed record TextLine(string Text, double X, double Y, double Width, double Height);
internal sealed record ScreenText(List<TextLine> Lines)
{
    public string Text => string.Join(Environment.NewLine, Lines.Select(l => l.Text));
}

internal sealed class OcrReader
{
    private readonly OcrEngine engine = OcrEngine.TryCreateFromLanguage(new Language("ru-RU"))
        ?? OcrEngine.TryCreateFromLanguage(new Language("en-US"))
        ?? OcrEngine.TryCreateFromUserProfileLanguages()
        ?? throw new InvalidOperationException("Нет установленного языка Windows OCR (русский или английский).");

    public async Task<ScreenText> ReadAsync(string imagePath)
    {
        var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(imagePath));
        using var stream = await file.OpenReadAsync();
        var decoder = await BitmapDecoder.CreateAsync(stream);
        var scale = decoder.PixelWidth < 2560 ? 2560.0 / decoder.PixelWidth : 1.0;
        using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore,
            new BitmapTransform { ScaledWidth = (uint)(decoder.PixelWidth * scale), ScaledHeight = (uint)(decoder.PixelHeight * scale), InterpolationMode = BitmapInterpolationMode.Cubic },
            ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);
        var result = await engine.RecognizeAsync(bitmap);
        return new ScreenText(result.Lines.Where(l => l.Words.Count > 0).Select(l =>
        {
            var left = l.Words.Min(w => w.BoundingRect.Left);
            var top = l.Words.Min(w => w.BoundingRect.Top);
            return new TextLine(l.Text, left / scale, top / scale,
                (l.Words.Max(w => w.BoundingRect.Right) - left) / scale,
                (l.Words.Max(w => w.BoundingRect.Bottom) - top) / scale);
        }).ToList());
    }
}

