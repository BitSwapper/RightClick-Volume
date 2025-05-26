using System;
using System.Collections.Generic;
using RightClickVolume.Models;

namespace RightClickVolume.Interfaces;

public interface IAudioManager : IDisposable
{
    AppAudioSession GetAudioSessionForProcess(uint targetProcessId);
    List<AppAudioSession> GetAllAudioSessions();
    AppAudioSession GetAudioSessionForWindow(IntPtr hwnd);
}
