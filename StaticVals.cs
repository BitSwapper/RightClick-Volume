using System.Diagnostics;

namespace RightClickVolume;

public static class StaticVals
{
    public const string AppName = "RightClick Volume";
    public static readonly string AppPath = Process.GetCurrentProcess().MainModule.FileName;
    public const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
}
