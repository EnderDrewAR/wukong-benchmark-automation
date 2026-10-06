using System.Text.Json;
using WukongBench;

GameWindow.SetProcessDpiAwarenessContext(-4);
Console.OutputEncoding = System.Text.Encoding.UTF8;
var json = new JsonSerializerOptions { WriteIndented = true };
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
try
{
    string? Option(string name) { var i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
    var command = args.FirstOrDefault() ?? "help";
    if (command == "help" || args.Contains("--help"))
    {
        Console.WriteLine("""
        WukongBench — два автоматических прохода Black Myth: Wukong Benchmark Tool
          run [--game-dir PATH] [--output PATH] [--profile cpu|gpu] [--no-rt] [--cpu-scale 25..100]
          inspect                  Снимок и OCR текущего окна для диагностики
          parse IMAGE              Прочитать ранее сохранённый экран результатов
        Во время run не переключайте окна. Ctrl+C отменяет запуск и восстанавливает настройки.
        """);
        return;
    }
    var ocr = new OcrReader();
    if (command == "parse")
    {
        var path = Path.GetFullPath(args[1]);
        var diagnostics = Path.GetFullPath(Option("--output") ?? Path.Combine(Path.GetTempPath(), "WukongBench-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(diagnostics);
        using var reader = new ResultReader();
        var screenText = await ocr.ReadAsync(path);
        File.WriteAllText(Path.Combine(diagnostics, "ocr.txt"), screenText.Text);
        Console.WriteLine(JsonSerializer.Serialize(reader.Read(path, screenText, diagnostics), json));
        return;
    }
    var gameDir = SteamInstallation.Find(Option("--game-dir"));
    if (command == "inspect")
    {
        using var process = GameWindow.Find(gameDir) ?? throw new InvalidOperationException("Бенчмарк не запущен.");
        Directory.CreateDirectory(".research");
        await GameWindow.FocusAsync(process, cancellation.Token);
        GameWindow.Capture(process, ".research/screen.png");
        var text = await ocr.ReadAsync(".research/screen.png");
        File.WriteAllText(".research/ocr.txt", text.Text);
        Console.WriteLine(text.Text);
        return;
    }
    if (command != "run") throw new ArgumentException("Неизвестная команда: " + command);
    using var existing = GameWindow.Find(gameDir);
    if (existing is not null) throw new InvalidOperationException("Перед запуском закройте уже открытый бенчмарк.");
    var output = Path.GetFullPath(Option("--output") ?? Path.Combine("artifacts", DateTime.Now.ToString("yyyyMMdd-HHmmss")));
    if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
        throw new InvalidOperationException("Папка результатов должна быть новой или пустой.");
    Directory.CreateDirectory(output);
    var settings = new SettingsManager(gameDir);
    var backup = settings.Backup(output);
    var original = File.ReadAllText(settings.FilePath);
    int ReadSetting(string key) => int.Parse(System.Text.RegularExpressions.Regex.Match(original, "(?m)^" + key + "=(\\d+)").Groups[1].Value);
    var width = ReadSetting("ResolutionSizeX");
    var height = ReadSetting("ResolutionSizeY");
    var cpuScale = int.Parse(Option("--cpu-scale") ?? "25");
    var cpuWidth = int.Parse(Option("--cpu-width") ?? "1280");
    var cpuHeight = int.Parse(Option("--cpu-height") ?? "720");
    if (cpuWidth < 640 || cpuHeight < 360 || cpuWidth > 3840 || cpuHeight > 2160)
        throw new ArgumentException("Недопустимое разрешение CPU-профиля.");
    if (cpuScale is < 25 or > 100) throw new ArgumentException("--cpu-scale должен быть от 25 до 100.");
    var selected = Option("--profile");
    if (selected is not null && selected is not "cpu" and not "gpu") throw new ArgumentException("--profile: cpu или gpu.");
    var profiles = new[] {
        new BenchmarkProfile("cpu", cpuWidth, cpuHeight, cpuScale, 0, false),
        new BenchmarkProfile("gpu", width, height, 100, 4, !args.Contains("--no-rt"))
    }.Where(p => selected is null || p.Name == selected);
    var runs = new List<RunReport>();
    var hardware = HardwareInfo.Collect();
    BenchmarkController? controller = null;
    try
    {
        foreach (var profile in profiles)
        {
            cancellation.Token.ThrowIfCancellationRequested();
            Console.WriteLine($"Профиль {profile.Name}: {profile.Width}×{profile.Height}, масштаб {profile.RenderPercent}%, качество {profile.Quality}, RT={profile.RayTracing}");
            var runDir = Path.Combine(output, profile.Name);
            Directory.CreateDirectory(runDir);
            settings.Apply(profile);
            File.Copy(settings.FilePath, Path.Combine(runDir, "applied-settings.ini"));
            controller = new BenchmarkController(gameDir, ocr);
            var result = await controller.RunAsync(runDir, cancellation.Token);
            File.Copy(settings.FilePath, Path.Combine(runDir, "effective-settings.ini"));
            runs.Add(new RunReport(profile, result));
            ReportWriter.Save(output, runs, hardware);
            Console.WriteLine($"{profile.Name}: средний {result.AverageFps}, мин. {result.MinimumFps}, макс. {result.MaximumFps}, P5 {result.FifthPercentileFps} FPS");
            await GameWindow.CloseAsync(controller.Process);
            controller.Process?.Dispose();
            controller = null;
            await Task.Delay(3000, cancellation.Token);
        }
        Console.WriteLine("Отчёт: " + Path.Combine(output, "report.html"));
    }
    catch (Exception e)
    {
        File.WriteAllText(Path.Combine(output, "error.txt"), e.ToString());
        throw;
    }
    finally
    {
        await GameWindow.CloseAsync(controller?.Process);
        controller?.Process?.Dispose();
        settings.Restore(backup);
        Console.WriteLine("Исходные настройки восстановлены.");
    }
}
catch (Exception e)
{
    Console.Error.WriteLine(e is OperationCanceledException ? "Запуск отменён." : e.Message);
    Environment.ExitCode = 1;
}
