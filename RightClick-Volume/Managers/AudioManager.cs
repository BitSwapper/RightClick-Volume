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
            Process process = Process.GetProcessById((int)processId);
            string processName = process.ProcessName;
            processIdToNameCache[processId] = processName;
            return processName;
        }
        catch(ArgumentException)
        {
            processIdToNameCache.Remove(processId);
            processIdToPathCache.Remove(processId);
            return null;
        }
        catch(InvalidOperationException)
        {
            processIdToNameCache.Remove(processId);
            processIdToPathCache.Remove(processId);
            return null;
        }
    }

    string GetProcessPathWithCaching(uint processId)
    {
        if(processIdToPathCache.TryGetValue(processId, out string cachedPath))
            return cachedPath;

        try
        {
            Process process = Process.GetProcessById((int)processId);
            string mainModulePath = process.MainModule?.FileName;
            if(!string.IsNullOrEmpty(mainModulePath))
            {
                processIdToPathCache[processId] = mainModulePath;
                return mainModulePath;
            }
        }
        catch(ArgumentException)
        {
            processIdToNameCache.Remove(processId);
            processIdToPathCache.Remove(processId);
        }
        catch(InvalidOperationException)
        {
            processIdToNameCache.Remove(processId);
            processIdToPathCache.Remove(processId);
        }
        catch(System.ComponentModel.Win32Exception ex)
        {
            Debug.WriteLine($"Win32Exception getting MainModule for PID {processId}: {ex.Message} (NativeErrorCode: {ex.NativeErrorCode})");
        }
        catch(NotSupportedException)
        {
            Debug.WriteLine($"NotSupportedException getting MainModule for PID {processId}.");
        }
        processIdToPathCache[processId] = string.Empty;
        return null;
    }

    public AppAudioSession GetAudioSessionForProcess(uint targetProcessId)
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
                AudioSessionControl session = null;
                try
                {
                    session = sessionEnumerator[i];
                    if(session.GetProcessID == targetProcessId)
                    {
                        string processName = GetProcessNameWithCaching(targetProcessId);
                        string displayName = session.DisplayName;

                        if(string.IsNullOrEmpty(displayName) && !string.IsNullOrEmpty(processName))
                        {
                            string processPath = GetProcessPathWithCaching(targetProcessId);
                            displayName = !string.IsNullOrEmpty(processPath) ? Path.GetFileNameWithoutExtension(processPath) : processName;
                        }
                        else if(string.IsNullOrEmpty(displayName))
                            displayName = "Unknown App";

                        return new AppAudioSession(session, displayName, targetProcessId);
                    }
                }
                catch(Exception ex)
                {
                    Debug.WriteLine($"Error processing session {i} for PID {targetProcessId}: {ex.Message}");
                }
                finally
                {
                    if(session != null && session.GetProcessID != targetProcessId)
                    {
                        session.Dispose();
                    }
                }
            }
        }
        catch(Exception ex)
        {
            Debug.WriteLine($"Error in GetAudioSessionForProcess(PID: {targetProcessId}): {ex.Message}");
            return null;
        }
        return null;
    }

    public List<AppAudioSession> GetAllAudioSessions()
    {
        var audioSessions = new List<AppAudioSession>();
        try
        {
            RefreshDefaultDevice();
            if(defaultPlaybackDevice == null) return audioSessions;

            AudioSessionManager sessionManager = defaultPlaybackDevice.AudioSessionManager;
            SessionCollection sessionEnumerator = sessionManager.Sessions;

            for(int i = 0; i < sessionEnumerator.Count; i++)
            {
                AudioSessionControl session = null;
                try
                {
                    session = sessionEnumerator[i];
                    if(session.State == AudioSessionState.AudioSessionStateExpired)
                    {
                        session.Dispose();
                        continue;
                    }

                    uint processId = session.GetProcessID;
                    if(processId == 0)
                    {
                        session.Dispose();
                        continue;
                    }


                    string processName = GetProcessNameWithCaching(processId);
                    string displayName = session.DisplayName;

                    if(string.IsNullOrEmpty(displayName) && !string.IsNullOrEmpty(processName))
                    {
                        string processPath = GetProcessPathWithCaching(processId);
                        displayName = !string.IsNullOrEmpty(processPath) ? Path.GetFileNameWithoutExtension(processPath) : processName;
                    }
                    else if(string.IsNullOrEmpty(displayName))
                        displayName = $"PID: {processId}";

                    audioSessions.Add(new AppAudioSession(session, displayName, processId));
                }
                catch(Exception ex)
                {
                    Debug.WriteLine($"Error processing session in GetAllAudioSessions: {ex.Message}");
                    session?.Dispose();
                }
            }
        }
        catch(Exception ex)
        {
            Debug.WriteLine($"Error in GetAllAudioSessions: {ex.Message}");
        }
        return audioSessions;
    }

    public AppAudioSession GetAudioSessionForWindow(IntPtr hwnd)
    {
        try
        {
            Native.WindowsInterop.GetWindowThreadProcessId(hwnd, out uint processId);
            if(processId == 0)
                return null;

            var session = GetAudioSessionForProcess(processId);
            if(session != null) return session;

            return TryGetParentWindowSession(hwnd, processId);
        }
        catch(Exception ex)
        {
            Debug.WriteLine($"Error in GetAudioSessionForWindow: {ex.Message}");
            return null;
        }
    }

    AppAudioSession TryGetParentWindowSession(IntPtr hwnd, uint childProcessId)
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
            return null;
        }
    }

    void RefreshDefaultDevice()
    {
        try
        {
            defaultPlaybackDevice?.Dispose();
            defaultPlaybackDevice = null;
            defaultPlaybackDevice = deviceEnumerator?.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
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