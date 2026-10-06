using System.Management;

namespace WukongBench;

internal static class HardwareInfo
{
    public static Dictionary<string, string> Collect()
    {
        var result = new Dictionary<string, string>();
        using (var cpu = new ManagementObjectSearcher("SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor"))
            foreach (ManagementObject item in cpu.Get())
                using (item) result["CPU"] = $"{item["Name"]}; {item["NumberOfCores"]} ядер, {item["NumberOfLogicalProcessors"]} потоков";
        using (var gpu = new ManagementObjectSearcher("SELECT Name, DriverVersion FROM Win32_VideoController"))
        {
            var index = 1;
            foreach (ManagementObject item in gpu.Get())
                using (item) result[$"GPU {index++}"] = $"{item["Name"]}; драйвер {item["DriverVersion"]}";
        }
        ulong bytes = 0;
        using (var memory = new ManagementObjectSearcher("SELECT Capacity FROM Win32_PhysicalMemory"))
            foreach (ManagementObject item in memory.Get())
                using (item) bytes += Convert.ToUInt64(item["Capacity"]);
        result["RAM"] = $"{bytes / (1024.0 * 1024 * 1024):0.##} GiB";
        result["OS"] = Environment.OSVersion.ToString();
        return result;
    }
}
