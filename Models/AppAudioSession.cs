using System;
using System.Diagnostics;
using NAudio.CoreAudioApi;

namespace RightClickVolume.Models;

public class AppAudioSession : IDisposable
{
    readonly AudioSessionControl _sessionControl;
    readonly SimpleAudioVolume _volumeControl;
    readonly AudioMeterInformation _meterInformation;
    bool _isDisposed = false;

    public uint ProcessId { get; private set; }
    public string DisplayName { get; private set; }


    public float Volume
    {
        get
        {
            if(_isDisposed) return 0f;
            try
            {
                return _volumeControl?.Volume ?? 0f;
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
            if(_isDisposed) return false;
            try
            {
                return _volumeControl?.Mute ?? false;
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
            if(_isDisposed) return 0f;
            try
            {
                float peak = _meterInformation?.MasterPeakValue ?? 0f;
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

        _sessionControl = sessionControl;
        _volumeControl = sessionControl.SimpleAudioVolume;
        _meterInformation = sessionControl.AudioMeterInformation;

        ProcessId = processId;

        DisplayName = !string.IsNullOrEmpty(displayName) ? displayName : $"PID: {processId}";

    }

    public void SetVolume(float volume)
    {
        if(_isDisposed) return;
        try
        {
            if(_volumeControl != null)
                _volumeControl.Volume = Math.Clamp(volume, 0f, 1f);
        }
        catch(Exception ex)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] AppAudioSession (PID: {ProcessId}, Name: {DisplayName}): ERROR setting Volume: {ex.Message}");
        }
    }

    public void SetMute(bool mute)
    {
        if(_isDisposed) return;
        try
        {
            if(_volumeControl != null)
                _volumeControl.Mute = mute;
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
        if(!_isDisposed)
        {
            if(disposing)
                _sessionControl?.Dispose();

            _isDisposed = true;
        }
    }

    ~AppAudioSession() => Dispose(false);
}