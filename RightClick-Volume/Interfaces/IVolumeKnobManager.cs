using System;
using RightClickVolume.Interfaces;

namespace RightClickVolume.Interfaces;

public interface IVolumeKnobManager : IDisposable
{
    void ShowKnobForSession(int clickX, int clickY, IAppAudioSession session);
    void HideAllKnobs();
    void StartCleanupTask();
    void StopCleanupTask();
}