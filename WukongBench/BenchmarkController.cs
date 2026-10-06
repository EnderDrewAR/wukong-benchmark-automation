using System.Diagnostics;

namespace WukongBench;

internal sealed class BenchmarkController(string gameDir, OcrReader ocr)
{
    public Process? Process { get; private set; }
    public async Task<BenchmarkResult> RunAsync(string directory, CancellationToken ct)
    {
        System.Diagnostics.Process.Start(new ProcessStartInfo("steam://rungameid/3132990") { UseShellExecute = true });
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromMinutes(2))
        {
            ct.ThrowIfCancellationRequested();
            Process ??= GameWindow.Find(gameDir);
            Process?.Refresh();
            if (Process?.MainWindowHandle != 0 && Process is not null) break;
            await Task.Delay(1000, ct);
        }
        if (Process is null || Process.MainWindowHandle == 0) throw new TimeoutException("Бенчмарк не открыл окно за 2 минуты.");
        var screenshot = Path.Combine(directory, "last-screen.png");
        var started = false;
        var confirmed = false;
        var lastAction = DateTime.MinValue;
        var lastStatus = "";
        using var reader = new ResultReader();
        timer.Restart();
        while (timer.Elapsed < TimeSpan.FromMinutes(12))
        {
            ct.ThrowIfCancellationRequested();
            if (Process.HasExited) throw new InvalidOperationException("Бенчмарк неожиданно завершился.");
            await GameWindow.FocusAsync(Process, ct);
            GameWindow.Capture(Process, screenshot);
            var text = await ocr.ReadAsync(screenshot);
            File.WriteAllText(Path.Combine(directory, "last-screen.txt"), text.Text);
            if (started && confirmed && ResultReader.IsResult(text))
            {
                await Task.Delay(1500, ct);
                GameWindow.Capture(Process, screenshot);
                text = await ocr.ReadAsync(screenshot);
                File.Copy(screenshot, Path.Combine(directory, "results.png"), true);
                File.WriteAllText(Path.Combine(directory, "results.txt"), text.Text);
                return reader.Read(screenshot, text, directory);
            }
            var status = confirmed ? "Выполняется проход; ожидаю экран результатов..." : "Ожидаю меню...";
            if (status != lastStatus) { Console.WriteLine(status); lastStatus = status; }
            if ((DateTime.UtcNow - lastAction).TotalSeconds > 3)
            {
                if (Contains(text, "Нажмите любую", "Press any"))
                {
                    await GameWindow.KeyAsync(Process, 0x0D, ct);
                    lastAction = DateTime.UtcNow;
                }
                else if (!confirmed && Contains(text, "Хотите запустить", "start the benchmark", "start benchmarking"))
                {
                    await GameWindow.KeyAsync(Process, 0x0D, ct);
                    started = confirmed = true;
                    lastAction = DateTime.UtcNow;
                }
                else if (!started)
                {
                    var menu = text.Lines.FirstOrDefault(l => l.Text.Equals("Тест быстродействия", StringComparison.OrdinalIgnoreCase)
                        || l.Text.Equals("Benchmark", StringComparison.OrdinalIgnoreCase));
                    if (menu is not null)
                    {
                        await GameWindow.ClickAsync(Process, menu, ct);
                        lastAction = DateTime.UtcNow;
                    }
                }
            }
            await Task.Delay(2500, ct);
        }
        throw new TimeoutException("Не получены результаты за 12 минут. См. last-screen.png и last-screen.txt.");
    }
    private static bool Contains(ScreenText text, params string[] values) => values.Any(v => text.Text.Contains(v, StringComparison.OrdinalIgnoreCase));
}
