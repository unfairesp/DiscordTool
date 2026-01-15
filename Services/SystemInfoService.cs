using System;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;

namespace DiscordManagementTool.Services;

public class SystemInfoService
{
    public string GetOS()
    {
        try
        {
            return RuntimeInformation.OSDescription;
        }
        catch { return "Unknown OS"; }
    }

    public string GetCPU()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
            var cpu = searcher.Get().Cast<ManagementBaseObject>().FirstOrDefault();
            return cpu?["Name"]?.ToString() ?? "Unknown CPU";
        }
        catch { return "Unknown CPU"; }
    }

    public string GetGPU()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
            var gpu = searcher.Get().Cast<ManagementBaseObject>().FirstOrDefault();
            return gpu?["Name"]?.ToString() ?? "Unknown GPU";
        }
        catch { return "Unknown GPU"; }
    }

    public string GetRAM()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
            var ram = searcher.Get().Cast<ManagementBaseObject>().FirstOrDefault();
            if (ram?["TotalPhysicalMemory"] != null)
            {
                long bytes = Convert.ToInt64(ram["TotalPhysicalMemory"]);
                return $"{bytes / 1024 / 1024 / 1024} GB";
            }
            return "Unknown RAM";
        }
        catch { return "Unknown RAM"; }
    }

    public string GetHWID()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT UUID FROM Win32_ComputerSystemProduct");
            var hwid = searcher.Get().Cast<ManagementBaseObject>().FirstOrDefault();
            return hwid?["UUID"]?.ToString() ?? "Unknown HWID";
        }
        catch { return "Unknown HWID"; }
    }

    public string GetComputerName() => Environment.MachineName;
    public string GetUserName() => Environment.UserName;
}
