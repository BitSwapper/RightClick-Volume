using System;

namespace RightClickVolume.Interfaces;

public interface IAppAudioSession : IDisposable
{
    uint ProcessId { get; }
    string DisplayName { get; }
    float Volume { get; }
    bool IsMuted { get; }
    float CurrentPeakValue { get; }

    void SetVolume(float volume);
    void SetMute(bool mute);
}