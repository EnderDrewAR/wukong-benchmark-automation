using System.Net;
using System.Text;
using System.Text.Json;

namespace WukongBench;

internal sealed record RunReport(BenchmarkProfile Profile, BenchmarkResult Result);
internal static class ReportWriter
{
    public static void Save(string output, List<RunReport> runs, Dictionary<string, string> hardware)
    {
        File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(new
        { CreatedAt = DateTimeOffset.Now, Hardware = hardware, Runs = runs }, new JsonSerializerOptions { WriteIndented = true }));
        static string E(string s) => WebUtility.HtmlEncode(s);
        var html = new StringBuilder("""
        <!doctype html><html lang="ru"><meta charset="utf-8"><meta name="viewport" content="width=device-width">
        <title>Wukong — результаты бенчмарка</title>
        <style>body{font:16px system-ui;max-width:1100px;margin:40px auto;padding:0 24px;background:#121716;color:#e6ede9}h1,h2{color:#80cbb3}table{border-collapse:collapse;width:100%}td,th{padding:12px;border-bottom:1px solid #35443e;text-align:left}img{width:100%;margin:20px 0}a{color:#80cbb3}</style>
        <h1>Black Myth: Wukong</h1><p>Автоматизированный запуск Benchmark Tool. Значения FPS прочитаны с итогового экрана приложения.</p>
        <table><tr><th>Профиль</th><th>Средний FPS</th><th>Минимум</th><th>Максимум</th><th>5-й перцентиль</th></tr>
        """);
        foreach (var r in runs) html.Append($"<tr><td>{E(r.Profile.Name)}</td><td>{r.Result.AverageFps}</td><td>{r.Result.MinimumFps}</td><td>{r.Result.MaximumFps}</td><td>{r.Result.FifthPercentileFps}</td></tr>");
        html.Append("</table><p>CPU-профиль уменьшает графическую нагрузку. Сам по себе он не доказывает ограничение производительности процессором; для проверки используйте контрольный прогон с другим масштабом рендера.</p>");
        html.Append("<h2>Компьютер (WMI)</h2><table>");
        foreach (var pair in hardware) html.Append($"<tr><td>{E(pair.Key)}</td><td>{E(pair.Value)}</td></tr>");
        html.Append("</table>");
        foreach (var r in runs)
        {
            var p = r.Profile;
            html.Append($"<section><h2>{E(p.Name)}</h2><p>Запрошено: {p.Width}×{p.Height}; масштаб {p.RenderPercent}%; качество {p.Quality} (0 — низкое, 4 — кинематографическое); трассировка: {p.RayTracing}. Генерация кадров и VSync отключены.</p><table>");
            html.Append("<tr><th colspan='2'>Распознанные поля экрана (исходный снимок ниже)</th></tr>");
            foreach (var pair in r.Result.ObservedFields.Where(p => !new[] { "Процессор", "Видеокарта", "Видеопамять", "Оперативная память", "Версия игры", "Драйвер видеокарты", "CPU", "GPU", "Memory" }.Contains(p.Key)))
                html.Append($"<tr><td>{E(pair.Key)}</td><td>{E(pair.Value)}</td></tr>");
            html.Append($"</table><p><a href='{p.Name}/effective-settings.ini'>Конфигурация после запуска</a> · <a href='{p.Name}/results.txt'>Распознанный текст</a></p><img alt='Экран результатов {p.Name}' src='{p.Name}/results.png'></section>");
        }
        html.Append("</html>");
        File.WriteAllText(Path.Combine(output, "report.html"), html.ToString());
    }
}
