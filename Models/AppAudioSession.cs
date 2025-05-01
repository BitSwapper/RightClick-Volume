using System;
using System.Diagnostics;

namespace RightClickVolume.Models;

public class AppAudioSession
{
    NAudio.CoreAudioApi.AudioSessionControl sessionControl;
    NAudio.CoreAudioApi.SimpleAudioVolume volumeControl;

    public uint ProcessId { get; private set; }
    public string ProcessName { get; private set; }
    public string DisplayName { get; private set; }
    public float Volume
    {
        get
        {
            try
            {
                return volumeControl?.Volume ?? 0f;
            }
            catch
            {
                return 0f;
            }
        }
    }

    public bool IsMuted
    {
        get
        {
            try
            {
                return volumeControl?.Mute ?? false;
            }
            catch
            {
                return false;
            }
        }
    }

    public AppAudioSession(NAudio.CoreAudioApi.AudioSessionControl sessionControl)
    {
        this.sessionControl = sessionControl;
        volumeControl = sessionControl.SimpleAudioVolume;

        ProcessId = sessionControl.GetProcessID;

        try
        {
            var process = Process.GetProcessById((int)ProcessId);
            ProcessName = process.ProcessName;
            DisplayName = string.IsNullOrEmpty(this.sessionControl.DisplayName) ? ProcessName : this.sessionControl.DisplayName;
        }
        catch
        {
            ProcessName = "Unknown";
            DisplayName = "Unknown Application";
        }
    }

    public void SetVolume(float volume)
    {
        try
        {
            volume = Math.Clamp(volume, 0f, 1f);
            volumeControl.Volume = volume;
        }
        catch { }
    }

    public void SetMute(bool mute)
    {
        try
        {
            volumeControl.Mute = mute;
        }
        catch { }
    }
}