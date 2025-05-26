using System;
using RightClickVolume.Native; 

namespace RightClickVolume.Interfaces;

public class GlobalHotkeyPressedEventArgs : EventArgs
{
    public int X { get; }
    public int Y { get; }
    public IntPtr WindowHandle { get; }

    public GlobalHotkeyPressedEventArgs(int x, int y, IntPtr windowHandle)
    {
        X = x;
        Y = y;
        WindowHandle = windowHandle;
    }
}


public interface IHotkeyService : IDisposable
{
    event EventHandler<GlobalHotkeyPressedEventArgs> GlobalHotkeyPressed;
    void StartMonitoring();
    void StopMonitoring();
}