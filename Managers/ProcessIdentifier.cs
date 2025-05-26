using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Automation;
using RightClickVolume.Interfaces;
using RightClickVolume.Native;

namespace RightClickVolume.Managers;

internal class ProcessIdentifier
{
    readonly uint currentProcessId;
    readonly IMappingManager mappingManager;

    public ProcessIdentifier(uint currentProcessId, IMappingManager mappingManager)
    {
        this.currentProcessId = currentProcessId;
        this.mappingManager = mappingManager ?? throw new ArgumentNullException(nameof(mappingManager));
    }

    public IdentificationResult IdentifyProcess(AutomationElement targetElement, string extractedName, CancellationToken cancellationToken)
    {
        string threadIdPrefix = $"   [BG Thread {Thread.CurrentThread.ManagedThreadId}]";

        var directResult = GetDirectPid(targetElement, threadIdPrefix);
        if(directResult.Success)
            return directResult;

        cancellationToken.ThrowIfCancellationRequested();
        var fallbackResult = GetFallbackPid(extractedName, threadIdPrefix, cancellationToken);
        if(fallbackResult.Success)
            return fallbackResult;

        cancellationToken.ThrowIfCancellationRequested();
        var mappedResult = GetMappedPid(extractedName, threadIdPrefix, cancellationToken);
        if(mappedResult.Success)
            return mappedResult;

        return new IdentificationResult { ProcessId = 0 };
    }

    IdentificationResult GetDirectPid(AutomationElement targetElement, string threadIdPrefix)
    {
        uint directPid = 0;
        string directPidSource = "None";
        bool allowExplorer = false;

        if(UiaHelper.TryGetElementHwnd(targetElement, out IntPtr elementHwnd) && elementHwnd != IntPtr.Zero)
        {
            WindowsInterop.GetWindowThreadProcessId(elementHwnd, out uint pidFromHwnd);
            if(pidFromHwnd != 0)
            {
                directPid = pidFromHwnd;
                directPidSource = $"HWND ({elementHwnd})";
                allowExplorer = true;
            }
        }

        if(directPid == 0 && UiaHelper.TryGetElementPid(targetElement, out uint pidFromUia))
            if(pidFromUia != 0)
            {
                directPid = pidFromUia;
                directPidSource = "UIA PID";
                allowExplorer = false;
            }

        if(directPid != 0)
            if(IsValidAppPid(directPid, $"Direct Check ({directPidSource})", allowExplorer, threadIdPrefix))
                return new IdentificationResult
                {
                    ProcessId = directPid,
                    ApplicationName = GetProcessNameSafe(directPid),
                    Method = "Direct"
                };

        return new IdentificationResult { ProcessId = 0 };
    }

    IdentificationResult GetFallbackPid(string extractedName, string threadIdPrefix, CancellationToken cancellationToken)
    {
        if(!string.IsNullOrWhiteSpace(extractedName) && extractedName != "[Error getting name]" && extractedName != "[Unknown]")
        {
            uint pidFromTitle = FindProcessIdByWindowTitle(extractedName, cancellationToken, threadIdPrefix);
            if(pidFromTitle != 0)
                return new IdentificationResult
                {
                    ProcessId = pidFromTitle,
                    ApplicationName = GetProcessNameSafe(pidFromTitle),
                    Method = "Window Title"
                };
        }

        return new IdentificationResult { ProcessId = 0 };
    }

    IdentificationResult GetMappedPid(string extractedName, string threadIdPrefix, CancellationToken cancellationToken)
    {
        if(string.IsNullOrWhiteSpace(extractedName) || extractedName == "[Error getting name]" || extractedName == "[Unknown]")
            return new IdentificationResult { ProcessId = 0 };

        var userMappings = mappingManager.LoadManualMappings();
        if(!userMappings.TryGetValue(extractedName, out List<string> mappedProcessNames) || mappedProcessNames.Count == 0)
            return new IdentificationResult { ProcessId = 0 };

        foreach(string mappedProcessName in mappedProcessNames)
        {
            if(cancellationToken.IsCancellationRequested) break;

            Process[] foundProcesses = null;
            try
            {
                foundProcesses = Process.GetProcessesByName(mappedProcessName);
            }
            catch
            {
                continue;
            }

            if(foundProcesses?.Length <= 0)
                continue;

            Process validProcess = null;
            uint potentialPid = 0;

            foreach(var currentProcess in foundProcesses)
            {
                if(cancellationToken.IsCancellationRequested) break;

                uint currentPotentialPid = 0;
                try
                {
                    currentPotentialPid = (uint)currentProcess.Id;
                    if(IsValidAppPid(currentPotentialPid, $"Manual Mapping Match ('{extractedName}' -> '{mappedProcessName}')", false, threadIdPrefix))
                    {
                        potentialPid = currentPotentialPid;
                        validProcess = currentProcess;
                        break;
                    }
                }
                catch
                {
                    try { currentProcess.Dispose(); } catch { }
                    continue;
                }

                if(validProcess != currentProcess)
                    try { currentProcess.Dispose(); } catch { }
            }

            if(validProcess != null && potentialPid != 0)
            {
                var result = new IdentificationResult
                {
                    ProcessId = potentialPid,
                    ApplicationName = validProcess.ProcessName,
                    Method = "Manual Mapping"
                };
                try { validProcess.Dispose(); } catch { }
                return result;
            }
            else
                foreach(var p in foundProcesses)
                    if(p != validProcess)
                        try { p.Dispose(); } catch { }
        }

        return new IdentificationResult { ProcessId = 0 };
    }

    uint FindProcessIdByWindowTitle(string extractedName, CancellationToken cancellationToken, string threadIdPrefix)
    {
        if(string.IsNullOrWhiteSpace(extractedName)) return 0;

        uint foundPid = 0;
        var matches = new List<(uint pid, IntPtr hwnd, string title, int score)>();

        WindowsInterop.EnumWindows((hWnd, lParam) =>
        {
            if(cancellationToken.IsCancellationRequested) return false;

            try
            {
                if(!WindowsInterop.IsWindowVisible(hWnd)) return true;

                int length = WindowsInterop.GetWindowTextLength(hWnd);
                if(length == 0) return true;

                int cloakedVal = 0;
                bool isCloaked = false;

                if(Environment.OSVersion.Version.Major >= 6 && Environment.OSVersion.Version.Minor >= 2)
                    try
                    {
                        int result = WindowsInterop.DwmGetWindowAttribute(hWnd, WindowsInterop.DWMWA_CLOAKED, ref cloakedVal, Marshal.SizeOf<int>());
                        isCloaked = (result == 0 && cloakedVal != 0);
                    }
                    catch { }

                if(isCloaked) return true;

                StringBuilder sb = new StringBuilder(length + 1);
                WindowsInterop.GetWindowText(hWnd, sb, sb.Capacity);
                string windowTitle = sb.ToString();
                WindowsInterop.GetWindowThreadProcessId(hWnd, out uint currentPid);

                if(currentPid != 0 && IsValidAppPid(currentPid, $"Window Title Enum Check ('{windowTitle}')", true, threadIdPrefix))
                {
                    int score = CalculateMatchScore(windowTitle, extractedName);
                    if(score > 0)
                        matches.Add((currentPid, hWnd, windowTitle, score));
                }
            }
            catch { }

            return true;
        }, IntPtr.Zero);

        if(cancellationToken.IsCancellationRequested)
            return 0;

        if(matches.Count != 0)
        {
            var bestMatch = matches.OrderByDescending(m => m.score).ThenBy(m => WindowsInterop.IsIconic(m.hwnd)).First();
            foundPid = bestMatch.pid;
        }

        return foundPid;
    }

    int CalculateMatchScore(string windowTitle, string extractedName)
    {
        if(windowTitle.Equals(extractedName, StringComparison.OrdinalIgnoreCase))
            return 100;
        else if(windowTitle.StartsWith(extractedName, StringComparison.OrdinalIgnoreCase))
            return 90;
        else if(windowTitle.Contains(extractedName, StringComparison.OrdinalIgnoreCase))
            return 70;
        else if(extractedName.Equals("Firefox", StringComparison.OrdinalIgnoreCase) && windowTitle.EndsWith("- Mozilla Firefox", StringComparison.OrdinalIgnoreCase))
            return 88;

        return 0;
    }

    bool IsValidAppPid(uint pid, string sourceDescription, bool allowExplorer, string threadIdPrefix)
    {
        if(pid == 0 || pid == currentProcessId) return false;

        try
        {
            using Process process = Process.GetProcessById((int)pid);
            if(process.HasExited) return false;

            string processName = process.ProcessName;
            var systemProcessNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "explorer", "svchost", "dwm", "csrss", "wininit", "services", "lsass", "smss", "System", "Idle", "Registry", "sihost", "ctfmon", "fontdrvhost", "ApplicationFrameHost", "ShellExperienceHost", "StartMenuExperienceHost", "SearchHost", "SearchApp", "SearchIndexer", "RuntimeBroker", "SecurityHealthSystray", "TextInputHost", "taskhostw", "dllhost" };

            if(systemProcessNames.Contains(processName))
            {
                bool explorerAllowedForThisSource = allowExplorer && processName.Equals("explorer", StringComparison.OrdinalIgnoreCase);
                if(!explorerAllowedForThisSource)
                    return false;
            }
            return true;
        }
        catch(ArgumentException) { return false; }
        catch(System.ComponentModel.Win32Exception ex)
        {
            if(ex.NativeErrorCode == 5)
                return true;
            return false;
        }
        catch(InvalidOperationException) { return false; }
        catch(Exception) { return false; }
    }

    string GetProcessNameSafe(uint pid)
    {
        try { using var process = Process.GetProcessById((int)pid); return process.ProcessName; }
        catch { return "Unknown"; }
    }

    public class IdentificationResult
    {
        public uint ProcessId { get; init; }
        public string ApplicationName { get; init; }
        public string Method { get; init; }
        public bool Success => ProcessId != 0;
    }
}