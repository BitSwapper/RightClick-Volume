using System;

namespace RightClickVolume.Interfaces;

public interface ITaskbarMonitor : IDisposable
{
    void StartMonitoring();
    void StopMonitoring();
}
