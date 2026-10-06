using Microsoft.Win32;
using System.Text.RegularExpressions;

namespace WukongBench;

internal static class SteamInstallation
{
    public static string Find(string? explicitPath)
    {
        if (explicitPath is not null) return Validate(Path.GetFullPath(explicitPath));
        var steam = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string
            ?? throw new InvalidOperationException("Steam не найден. Передайте --game-dir <путь>.");
        var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { steam };
        var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
        if (File.Exists(vdf))
            foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                libraries.Add(m.Groups[1].Value.Replace(@"\\", @"\"));
        foreach (var library in libraries)
        {
            var manifest = Path.Combine(library, "steamapps", "appmanifest_3132990.acf");
            if (!File.Exists(manifest)) continue;
            var match = Regex.Match(File.ReadAllText(manifest), "\"installdir\"\\s+\"([^\"]+)\"");
            if (match.Success) return Validate(Path.Combine(library, "steamapps", "common", match.Groups[1].Value));
        }
        throw new FileNotFoundException("Black Myth: Wukong Benchmark Tool (3132990) не установлен.");
    }

    private static string Validate(string path) => File.Exists(Path.Combine(path, "b1_benchmark.exe"))
        ? Path.GetFullPath(path) : throw new FileNotFoundException("Не найден b1_benchmark.exe в " + path);
}
