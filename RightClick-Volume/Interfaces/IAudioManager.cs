using System;
using System.Collections.Generic;
using RightClickVolume.Models;

namespace RightClickVolume.Interfaces;

public interface IAudioManager : IDisposable
{
    IAppAudioSession GetAudioSessionForProcess(uint targetProcessId);
    List<IAppAudioSession> GetAllAudioSessions();
    IAppAudioSession GetAudioSessionForWindow(IntPtr hwnd);
}