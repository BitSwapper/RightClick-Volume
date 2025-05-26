using System;
using RightClickVolume.Native;

namespace RightClickVolume.Interfaces;

public interface IWindowsHookService : IDisposable
{
    event EventHandler<MouseHookEventArgs> RightMouseClick;
    void InstallMouseHook();
    void UninstallMouseHook();
}