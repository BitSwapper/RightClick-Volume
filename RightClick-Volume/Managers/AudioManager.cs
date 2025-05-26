using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using RightClickVolume.Interfaces;
using RightClickVolume.Models;

namespace RightClickVolume.Managers;

public class AudioManager : IAudioManager
{
    MMDeviceEnumerator deviceEnumerator;
    MMDevice defaultPlaybackDevice;
    readonly Dictionary<uint, string> processIdToNameCache = new Dictionary<uint, string>();
    readonly Dictionary<uint, string> processIdToPathCache = new Dictionary<uint, string>();

    public AudioManager()
    {
        try
        {
            deviceEnumerator = new MMDeviceEnumerator();
            RefreshDefaultDevice();
        }
        catch(Exception ex)
        {
            Debug.WriteLine($"AudioManager Critical Initialization Failed: {ex.Message}");
            throw;
        }
    }

    string GetProcessNameWithCaching(uint processId)
    {
        if(processIdToNameCache.TryGetValue(processId, out string cachedName))
            return cachedName;
        try
        {
            using Process process = Process.GetProcessById((int)processId);
            string processName = process.ProcessName;
            processIdToNameCache[processId] = processName;
            return processName;
        }
        catch { }
        return null;
    }

    string GetProcessPathWithCaching(uint processId)
    {
        if(processIdToPathCache.TryGetValue(processId, out string cachedPath))
            return cachedPath;
        try
        {
            using Process process = Process.GetProcessById((int)processId);
            string mainModulePath = process.MainModule?.FileName;
            if(!string.IsNullOrEmpty(mainModulePath))
            {
                processIdToPathCache[processId] = mainModulePath;
                return mainModulePath;
            }
        }
        catch { }
        processIdToPathCache[processId] = string.Empty;
        return null;
    }

    public IAppAudioSession GetAudioSessionForProcess(uint targetProcessId)
    {
        if(targetProcessId == 0) return null;
        try
        {
            RefreshDefaultDevice();
            if(defaultPlaybackDevice == null) return null;

            AudioSessionManager sessionManager = defaultPlaybackDevice.AudioSessionManager;
            SessionCollection sessionEnumerator = sessionManager.Sessions;

            for(int i = 0; i < sessionEnumerator.Count; i++)
            {
                AudioSessionControl sessionControl = null;
                try
                {
                    sessionControl = sessionEnumerator[i];
                    if(sessionControl.GetProcessID == targetProcessId)
                    {
                        string processName = GetProcessNameWithCaching(targetProcessId);
                        string displayName = sessionControl.DisplayName;

                        if(string.IsNullOrEmpty(displayName) && !string.IsNullOrEmpty(processName))
                        {
                            string processPath = GetProcessPathWithCaching(targetProcessId);
                            displayName = !string.IsNullOrEmpty(processPath) ? Path.GetFileNameWithoutExtension(processPath) : processName;
                        }
                        else if(string.IsNullOrEmpty(displayName))
                            displayName = "Unknown App";

                        return new AppAudioSession(sessionControl, displayName, targetProcessId);
                    }
                    else
                    {
                        sessionControl.Dispose();
                        sessionControl = null;
                    }
                }
                catch(Exception ex)
                {
                    Debug.WriteLine($"Error processing session {i} for PID {targetProcessId}: {ex.Message}");
                    sessionControl?.Dispose();
                }
            }
        }
        catch(Exception ex)
        {
            Debug.WriteLine($"Error in GetAudioSessionForProcess(PID: {targetProcessId}): {ex.Message}");
        }
        return null;
    }

    public List<IAppAudioSession> GetAllAudioSessions()
    {
        var audioSessions = new List<IAppAudioSession>();
        try
        {
            RefreshDefaultDevice();
            if(defaultPlaybackDevice == null) return audioSessions;

            AudioSessionManager sessionManager = defaultPlaybackDevice.AudioSessionManager;
            SessionCollection sessionEnumerator = sessionManager.Sessions;

            for(int i = 0; i < sessionEnumerator.Count; i++)
            {
                AudioSessionControl sessionControl = null;
                try
                {
                    sessionControl = sessionEnumerator[i];
                    if(sessionControl.State == AudioSessionState.AudioSessionStateExpired)
                    {
                        sessionControl.Dispose();
                        continue;
                    }
                    uint processId = sessionControl.GetProcessID;
                    if(processId == 0)
                    {
                        sessionControl.Dispose();
                        continue;
                    }
                    string processName = GetProcessNameWithCaching(processId);
                    string displayName = sessionControl.DisplayName;

                    if(string.IsNullOrEmpty(displayName) && !string.IsNullOrEmpty(processName))
                    {
                        string processPath = GetProcessPathWithCaching(processId);
                        displayName = !string.IsNullOrEmpty(processPath) ? Path.GetFileNameWithoutExtension(processPath) : processName;
                    }
                    else if(string.IsNullOrEmpty(displayName))
                        displayName = $"PID: {processId}";

                    audioSessions.Add(new AppAudioSession(sessionControl, displayName, processId));
                }
                catch(Exception ex)
                {
                    Debug.WriteLine($"Error processing session in GetAllAudioSessions: {ex.Message}");
                    sessionControl?.Dispose();
                }
            }
        }
        catch(Exception ex)
        {
            Debug.WriteLine($"Error in GetAllAudioSessions: {ex.Message}");
        }
        return audioSessions;
    }

    public IAppAudioSession GetAudioSessionForWindow(IntPtr hwnd)
    {
        try
        {
            Native.WindowsInterop.GetWindowThreadProcessId(hwnd, out uint processId);
            if(processId == 0) return null;

            IAppAudioSession session = GetAudioSessionForProcess(processId);
            if(session != null) return session;

            return TryGetParentWindowSession(hwnd, processId);
        }
        catch(Exception ex)
        {
            Debug.WriteLine($"Error in GetAudioSessionForWindow: {ex.Message}");
        }
        return null;
    }

    IAppAudioSession TryGetParentWindowSession(IntPtr hwnd, uint childProcessId)
    {
        try
        {
            var guiInfo = new Native.GUITHREADINFO();
            guiInfo.cbSize = Marshal.SizeOf(guiInfo);
            if(!Native.AudioInterop.GetGUIThreadInfo(0, ref guiInfo)) return null;
            if(guiInfo.hwndActive == IntPtr.Zero || guiInfo.hwndActive == hwnd) return null;

            Native.WindowsInterop.GetWindowThreadProcessId(guiInfo.hwndActive, out uint parentProcessId);
            if(parentProcessId == 0 || parentProcessId == childProcessId) return null;

            return GetAudioSessionForProcess(parentProcessId);
        }
        catch(Exception ex)
        {
            Debug.WriteLine($"Error in TryGetParentWindowSession: {ex.Message}");
        }
        return null;
    }

    void RefreshDefaultDevice()
    {
        try
        {
            defaultPlaybackDevice?.Dispose();
            defaultPlaybackDevice = null;
            if(deviceEnumerator != null)
            {
                defaultPlaybackDevice = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            }
        }
        catch(Exception ex)
        {
            Debug.WriteLine($"Failed to refresh default audio device: {ex.Message}");
            defaultPlaybackDevice = null;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if(disposing)
        {
            defaultPlaybackDevice?.Dispose();
            defaultPlaybackDevice = null;
            deviceEnumerator?.Dispose();
            deviceEnumerator = null;
            processIdToNameCache.Clear();
            processIdToPathCache.Clear();
        }
    }
}