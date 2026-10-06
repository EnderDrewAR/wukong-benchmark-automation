using System.Text.RegularExpressions;

namespace WukongBench;

internal sealed record BenchmarkProfile(string Name, int Width, int Height, int RenderPercent, int Quality, bool RayTracing);

internal sealed class SettingsManager(string gameDir)
{
    public string FilePath { get; } = Path.Combine(gameDir, "b1", "Saved", "Config", "Windows", "GameUserSettings.ini");
    public byte[] Backup(string directory)
    {
        if (!File.Exists(FilePath)) throw new FileNotFoundException("Один раз запустите бенчмарк и завершите первичную настройку.", FilePath);
        var bytes = File.ReadAllBytes(FilePath);
        File.WriteAllBytes(Path.Combine(directory, "original-GameUserSettings.ini"), bytes);
        return bytes;
    }
    public void Restore(byte[] bytes) => File.WriteAllBytes(FilePath, bytes);

    public void Apply(BenchmarkProfile p)
    {
        var text = File.ReadAllText(FilePath);
        foreach (var key in new[] { "ResolutionSizeX", "LastUserConfirmedResolutionSizeX" }) text = Set(text, key, p.Width.ToString());
        foreach (var key in new[] { "ResolutionSizeY", "LastUserConfirmedResolutionSizeY" }) text = Set(text, key, p.Height.ToString());
        text = Set(text, "bUseVSync", "False");
        foreach (var key in new[] { "FullscreenMode", "LastConfirmedFullscreenMode", "PreferredFullscreenMode" })
            text = Set(text, key, p.Name == "cpu" ? "2" : "1");
        text = Set(text, "bUseDynamicResolution", "False");
        text = Set(text, "FrameRateLimit", "0.000000");
        text = Set(text, "sg.ResolutionQuality", p.RenderPercent.ToString());
        foreach (var key in new[] { "ViewDistance", "AntiAliasing", "Shadow", "GlobalIllumination", "Reflection", "PostProcess", "Texture", "Effects", "Foliage", "Shading" })
            text = Set(text, $"sg.{key}Quality", p.Quality.ToString());
        text = Set(text, "sg.RayTracingQuality", p.RayTracing ? "3" : "0");
        var ui = new Dictionary<string, string>
        {
            ["Vsync"] = "0", ["LockFrameRate"] = "0", ["InsertFrame"] = "0",
            ["Rtx"] = p.RayTracing ? "1" : "0", ["RtxLevel"] = "3",
            ["ImageQuality"] = (p.Height * p.RenderPercent / 100).ToString(),
            ["ScreenMode"] = p.Name == "cpu" ? "2" : "1",
            ["MotionBlur"] = "0", ["QualityLevel"] = "6"
        };
        foreach (var key in new[] { "ViewDistance", "AntiAliasing", "PostProcessing", "ShadowQuality", "TextureQuality", "FxQuality", "MaterialQuality", "VegetationQuality", "GlobalIllumination", "ReflectionQuality" })
            ui[key] = (p.Quality + 1).ToString();
        foreach (var pair in ui)
        {
            var pattern = "(\\(\"" + Regex.Escape(pair.Key) + "\",\\s*\")[^\"]*(\"\\))";
            if (!Regex.IsMatch(text, pattern)) throw new InvalidOperationException("Неизвестная версия настроек: отсутствует " + pair.Key);
            text = Regex.Replace(text, pattern, m => m.Groups[1].Value + pair.Value + m.Groups[2].Value);
        }
        File.WriteAllText(FilePath, text);
    }

    internal static string Set(string text, string key, string value)
    {
        var pattern = "(?m)^" + Regex.Escape(key) + "=[^\\r\\n]*";
        if (!Regex.IsMatch(text, pattern)) throw new InvalidOperationException("В конфигурации отсутствует " + key);
        return Regex.Replace(text, pattern, _ => key + "=" + value);
    }
}
