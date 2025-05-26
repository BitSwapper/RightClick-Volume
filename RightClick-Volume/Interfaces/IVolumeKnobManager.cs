using System;
using RightClickVolume.Models;

namespace RightClickVolume.Interfaces;

public interface IVolumeKnobManager : IDisposable
{
    void ShowKnobForSession(int clickX, int clickY, AppAudioSession session);
    void HideAllKnobs();
    void StartCleanupTask();
    void StopCleanupTask();
}