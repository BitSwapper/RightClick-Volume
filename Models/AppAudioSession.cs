using System;
using System.Diagnostics;
using NAudio.CoreAudioApi;

namespace RightClickVolume.Models;

public class AppAudioSession
{
    readonly AudioSessionControl _sessionControl;
    readonly SimpleAudioVolume _volumeControl;
    readonly AudioMeterInformation _meterInformation;

    public uint ProcessId { get; private set; }
    public string ProcessName { get; private set; }
    public string DisplayName { get; private set; }

    public float Volume
    {
        get
        {
            try
            {
                return _volumeControl?.Volume ?? 0f;
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
                return _volumeControl?.Mute ?? false;
            }
            catch
            {
                return false;
            }
        }
    }

    public float CurrentPeakValue
    {
        get
        {
            try
            {
                float peak = _meterInformation?.MasterPeakValue ?? 0f;
                if(ProcessId != 0)
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] AppAudioSession (PID: {ProcessId}, Name: {DisplayName}): Get CurrentPeakValue -> Raw MasterPeakValue = {peak:F5}");
                }
                return peak;
            }
            catch(Exception ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] AppAudioSession (PID: {ProcessId}, Name: {DisplayName}): ERROR in CurrentPeakValue getter: {ex.Message}");
                return 0f;
            }
        }
    }

    public AppAudioSession(AudioSessionControl sessionControl)
    {
        if(sessionControl == null)
            throw new ArgumentNullException(nameof(sessionControl));

        _sessionControl = sessionControl;
        _volumeControl = sessionControl.SimpleAudioVolume;
        _meterInformation = sessionControl.AudioMeterInformation;

        ProcessId = sessionControl.GetProcessID;

        try
        {
            if(ProcessId == 0)
            {
                ProcessName = "System Sounds";
                DisplayName = !string.IsNullOrEmpty(_sessionControl.DisplayName) ? _sessionControl.DisplayName : ProcessName;
            }
            else
            {
                using var process = Process.GetProcessById((int)ProcessId);
                ProcessName = process.ProcessName;
                DisplayName = string.IsNullOrEmpty(_sessionControl.DisplayName) ? ProcessName : _sessionControl.DisplayName;
            }
        }
        catch(ArgumentException)
        {
            ProcessName = "Unknown (Exited)";
            DisplayName = !string.IsNullOrEmpty(_sessionControl.DisplayName) ? _sessionControl.DisplayName : "Unknown Application";
        }
        catch(InvalidOperationException)
        {
            ProcessName = "Unknown (Access Denied/Exited)";
            DisplayName = !string.IsNullOrEmpty(_sessionControl.DisplayName) ? _sessionControl.DisplayName : "Unknown Application";
        }
        catch(Exception ex)
        {
            ProcessName = "Unknown";
            DisplayName = !string.IsNullOrEmpty(_sessionControl.DisplayName) ? _sessionControl.DisplayName : "Unknown Application";
        }
    }

    public void SetVolume(float volume)
    {
        try
        {
            if(_volumeControl != null)
            {
                _volumeControl.Volume = Math.Clamp(volume, 0f, 1f);
            }
        }
        catch(Exception ex)
        {
        }
    }

    public void SetMute(bool mute)
    {
        try
        {
            if(_volumeControl != null)
            {
                _volumeControl.Mute = mute;
            }
        }
        catch(Exception ex)
        {
        }
    }
}