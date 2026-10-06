using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace WukongBench;

internal static class GameWindow
{
    [DllImport("user32.dll")] private static extern bool GetClientRect(nint h, out Rect r);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(nint h, ref Point p);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint h);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint h, out uint processId);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint from, uint to, bool attach);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint h, int command);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll")] internal static extern bool SetProcessDpiAwarenessContext(nint context);
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public InputUnion Data; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion
    {
        [FieldOffset(0)] public MouseInput Mouse;
        [FieldOffset(0)] public KeyboardInput Keyboard;
    }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput
    { public int X, Y; public uint Data, Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput
    { public ushort Vk, Scan; public uint Flags, Time; public nuint Extra; }

    public static Process? Find(string gameDir)
    {
        var expected = Path.Combine(gameDir, "b1", "Binaries", "Win64", "b1-Win64-Shipping.exe");
        foreach (var p in Process.GetProcessesByName("b1-Win64-Shipping"))
        {
            if (string.Equals(p.MainModule?.FileName, expected, StringComparison.OrdinalIgnoreCase)) return p;
            p.Dispose();
        }
        return null;
    }

    public static async Task FocusAsync(Process process, CancellationToken ct)
    {
        process.Refresh();
        if (process.HasExited || process.MainWindowHandle == 0) throw new InvalidOperationException("Нет окна бенчмарка.");
        for (var attempt = 0; attempt < 3 && GetForegroundWindow() != process.MainWindowHandle; attempt++)
        {
            var currentThread = GetCurrentThreadId();
            var foregroundThread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
            var attached = foregroundThread != currentThread && AttachThreadInput(currentThread, foregroundThread, true);
            try
            {
                ShowWindow(process.MainWindowHandle, 9);
                SetForegroundWindow(process.MainWindowHandle);
            }
            finally { if (attached) AttachThreadInput(currentThread, foregroundThread, false); }
            await Task.Delay(350, ct);
        }
        if (GetForegroundWindow() != process.MainWindowHandle)
            throw new InvalidOperationException("Не удалось активировать окно бенчмарка. Закройте перекрывающие диалоги.");
    }

    public static void Capture(Process process, string path)
    {
        var h = process.MainWindowHandle;
        if (GetForegroundWindow() != h) throw new InvalidOperationException("Окно бенчмарка потеряло фокус.");
        if (!GetClientRect(h, out var r) || r.Right < 100 || r.Bottom < 100)
            throw new InvalidOperationException("Неверные размеры окна.");
        var origin = new Point();
        if (!ClientToScreen(h, ref origin)) throw new InvalidOperationException("Не удалось определить положение окна.");
        using var bitmap = new Bitmap(r.Right, r.Bottom);
        using (var graphics = Graphics.FromImage(bitmap))
            graphics.CopyFromScreen(origin, Point.Empty, bitmap.Size);
        bitmap.Save(path, ImageFormat.Png);
    }

    public static async Task KeyAsync(Process process, ushort key, CancellationToken ct)
    {
        await FocusAsync(process, ct);
        Send(new Input { Type = 1, Data = new InputUnion { Keyboard = new KeyboardInput { Vk = key } } });
        try { await Task.Delay(80, ct); }
        finally { Send(new Input { Type = 1, Data = new InputUnion { Keyboard = new KeyboardInput { Vk = key, Flags = 2 } } }); }
    }

    public static async Task ClickAsync(Process process, TextLine line, CancellationToken ct)
    {
        await FocusAsync(process, ct);
        var point = new Point((int)(line.X + line.Width / 2), (int)(line.Y + line.Height / 2));
        if (!ClientToScreen(process.MainWindowHandle, ref point)) throw new InvalidOperationException("ClientToScreen failed.");
        SetCursorPos(point.X, point.Y);
        Send(new Input { Data = new InputUnion { Mouse = new MouseInput { Flags = 2 } } });
        try { await Task.Delay(80, ct); }
        finally { Send(new Input { Data = new InputUnion { Mouse = new MouseInput { Flags = 4 } } }); }
    }

    private static void Send(Input input)
    {
        if (SendInput(1, [input], Marshal.SizeOf<Input>()) != 1)
            throw new InvalidOperationException("Windows не приняла ввод в окно бенчмарка.");
    }

    public static async Task CloseAsync(Process? process)
    {
        if (process is null || process.HasExited) return;
        process.CloseMainWindow();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }
    }
}
