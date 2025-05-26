using System;
using System.Diagnostics;
using NAudio.CoreAudioApi;
using RightClickVolume.Interfaces;

namespace RightClickVolume.Models;

public class AppAudioSession : IAppAudioSession
{
    readonly AudioSessionControl sessionControl;
    readonly SimpleAudioVolume volumeControl;
    readonly AudioMeterInformation meterInformation;
    bool isDisposed = false;

    public uint ProcessId { get; private set; }
    public string DisplayName { get; private set; }

    public float Volume
    {
        get
        {
            if(isDisposed) return 0f;
            try
            {
                return volumeControl?.Volume ?? 0f;
            }
            catch(Exception ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] AppAudioSession (PID: {ProcessId}, Name: {DisplayName}): ERROR getting Volume: {ex.Message}");
                return 0f;
            }
        }
    }

    public bool IsMuted
    {
        get
        {
            if(isDisposed) return false;
            try
            {
                return volumeControl?.Mute ?? false;
            }
            catch(Exception ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] AppAudioSession (PID: {ProcessId}, Name: {DisplayName}): ERROR getting Mute state: {ex.Message}");
                return false;
            }
        }
    }

    public float CurrentPeakValue
    {
        get
        {
            if(isDisposed) return 0f;
            try
            {
                float peak = meterInformation?.MasterPeakValue ?? 0f;
                return peak;
            }
            catch(Exception ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] AppAudioSession (PID: {ProcessId}, Name: {DisplayName}): ERROR in CurrentPeakValue getter: {ex.Message}");
                return 0f;
            }
        }
    }

    public AppAudioSession(AudioSessionControl sessionControl, string displayName, uint processId)
    {
        if(sessionControl == null)
            throw new ArgumentNullException(nameof(sessionControl));

        this.sessionControl = sessionControl;
        try
        {
            volumeControl = sessionControl.SimpleAudioVolume;
            meterInformation = sessionControl.AudioMeterInformation;
        }
        catch(Exception ex)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] AppAudioSession (PID: {processId}, Name: {displayName}): ERROR getting Volume/Meter controls: {ex.Message}");
            // Allow construction but it might be partially functional
        }


        ProcessId = processId;
        DisplayName = !string.IsNullOrEmpty(displayName) ? displayName : $"PID: {processId}";
    }

    public void SetVolume(float volume)
    {
        if(isDisposed || volumeControl == null) return;
        try
        {
            volumeControl.Volume = Math.Clamp(volume, 0f, 1f);
        }
        catch(Exception ex)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] AppAudioSession (PID: {ProcessId}, Name: {DisplayName}): ERROR setting Volume: {ex.Message}");
        }
    }

    public void SetMute(bool mute)
    {
        if(isDisposed || volumeControl == null) return;
        try
        {
            volumeControl.Mute = mute;
        }
        catch(Exception ex)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] AppAudioSession (PID: {ProcessId}, Name: {DisplayName}): ERROR setting Mute: {ex.Message}");
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if(!isDisposed)
        {
            if(disposing)
            {
                sessionControl?.Dispose();
            }
            isDisposed = true;
        }
    }

    ~AppAudioSession() => Dispose(false);
}