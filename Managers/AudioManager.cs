using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using RightClickVolume.Models;

namespace RightClickVolume.Managers;

public class AudioManager : IDisposable
{
    MMDeviceEnumerator deviceEnumerator;
    MMDevice defaultPlaybackDevice;
    readonly Dictionary<uint, string> processExecutables = new Dictionary<uint, string>();

    public AudioManager()
    {
        try
        {
            deviceEnumerator = new MMDeviceEnumerator();
            defaultPlaybackDevice = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            UpdateProcessExecutables();
            GetAllAudioSessions();
        }
        catch
        {
            throw;
        }
    }

    void UpdateProcessExecutables()
    {
        try
        {
            foreach(var process in Process.GetProcesses())
                try
                {
                    if(!processExecutables.ContainsKey((uint)process.Id))
                        processExecutables[(uint)process.Id] = process.MainModule?.FileName ?? process.ProcessName;
                }
                catch { }
        }
        catch { }
    }

    public AppAudioSession GetAudioSessionForProcess(uint processId)
    {
        try
        {
            RefreshDefaultDevice();
            var sessionManager = defaultPlaybackDevice.AudioSessionManager;
            var sessionEnumerator = sessionManager.Sessions;

            for(int i = 0; i < sessionEnumerator.Count; i++)
            {
                var session = sessionEnumerator[i];
                if(session.GetProcessID == processId)
                    return new AppAudioSession(session);
            }

            return FindSessionByProcessNameMatch(processId, sessionEnumerator);
        }
        catch
        {
            return null;
        }
    }

    AppAudioSession FindSessionByProcessNameMatch(uint processId, SessionCollection sessionEnumerator)
    {
        string processPath = null;
        string processName = null;
        try
        {
            if(processExecutables.TryGetValue(processId, out processPath))
                processName = System.IO.Path.GetFileNameWithoutExtension(processPath);
            else
            {
                var process = Process.GetProcessById((int)processId);
                processName = process.ProcessName;
            }
        }
        catch { }

        if(string.IsNullOrEmpty(processName))
            return null;

        for(int i = 0; i < sessionEnumerator.Count; i++)
        {
            var session = sessionEnumerator[i];
            try
            {
                uint sessionProcessId = session.GetProcessID;
                string sessionProcessName = GetProcessNameById((int)sessionProcessId);

                if(sessionProcessName != null && sessionProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase))
                    return new AppAudioSession(session);

                if(sessionProcessName != null && (sessionProcessName.Contains(processName, StringComparison.OrdinalIgnoreCase) ||
                   processName.Contains(sessionProcessName, StringComparison.OrdinalIgnoreCase)))
                    return new AppAudioSession(session);
            }
            catch { }
        }

        return null;
    }

    string GetProcessNameById(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch { }
        return null;
    }

    public List<AppAudioSession> GetAllAudioSessions()
    {
        var audioSessions = new List<AppAudioSession>();
        try
        {
            RefreshDefaultDevice();
            UpdateProcessExecutables();
            var sessionManager = defaultPlaybackDevice.AudioSessionManager;
            var sessionEnumerator = sessionManager.Sessions;

            for(int i = 0; i < sessionEnumerator.Count; i++)
            {
                var session = sessionEnumerator[i];
                if(session.State != AudioSessionState.AudioSessionStateExpired)
                    try { audioSessions.Add(new AppAudioSession(session)); }
                    catch { }
            }
        }
        catch { }
        return audioSessions;
    }

    public AppAudioSession GetAudioSessionForWindow(IntPtr hwnd)
    {
        try
        {
            Native.WindowsInterop.GetWindowThreadProcessId(hwnd, out uint processId);
            if(processId == 0)
                return null;

            string processName = GetProcessNameOrDefault((int)processId, "Unknown");

            var session = GetAudioSessionForProcess(processId);
            if(session != null) return session;

            return TryGetParentWindowSession(hwnd, processId);
        }
        catch
        {
            return null;
        }
    }

    string GetProcessNameOrDefault(int processId, string defaultName)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch { }
        return defaultName;
    }

    AppAudioSession TryGetParentWindowSession(IntPtr hwnd, uint processId)
    {
        try
        {
            var guiInfo = new Native.GUITHREADINFO();
            guiInfo.cbSize = Marshal.SizeOf(guiInfo);
            if(!Native.AudioInterop.GetGUIThreadInfo(0, ref guiInfo)) return null;
            if(guiInfo.hwndActive == IntPtr.Zero || guiInfo.hwndActive == hwnd) return null;

            Native.WindowsInterop.GetWindowThreadProcessId(guiInfo.hwndActive, out uint parentProcessId);
            if(parentProcessId == 0 || parentProcessId == processId) return null;

            return GetAudioSessionForProcess(parentProcessId);
        }
        catch
        {
            return null;
        }
    }

    void RefreshDefaultDevice()
    {
        try
        {
            defaultPlaybackDevice?.Dispose();
            defaultPlaybackDevice = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        }
        catch { }
    }

    public void Dispose()
    {
        defaultPlaybackDevice?.Dispose();
        deviceEnumerator?.Dispose();
    }
}