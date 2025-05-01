using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Automation;

namespace RightClickVolume.Managers;

internal static class UiaHelper
{
    static readonly Regex NameExtractionRegex = new(@"^(.*?)(?:\s*-\s*\d+\s+running\s+window(?:s)?)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.RightToLeft);

    public static string GetElementDebugInfo(AutomationElement element)
    {
        if(element == null) return "[Null Element]";
        string name = GetElementNameSafe(element);
        string type = GetControlTypeSafe(element)?.ProgrammaticName ?? "N/A";
        string className = GetClassNameSafe(element);
        string pid = TryGetElementPid(element, out uint p) ? p.ToString() : "N/A";
        string hwnd = TryGetElementHwnd(element, out IntPtr h) ? h.ToString("X") : "N/A";
        return $"Name='{name}', Type='{type}', Class='{className}', PID={pid}, HWND=0x{hwnd}";
    }

    public static string GetClassNameSafe(AutomationElement element)
    {
        try { return element?.Current.ClassName; }
        catch(Exception ex) when(IsUiaElementAccessException(ex)) { return "[Error getting ClassName]"; }
    }

    public static bool TryGetElementPid(AutomationElement element, out uint pid)
    {
        pid = 0;
        try
        {
            if(element == null) return false;
            pid = (uint)element.Current.ProcessId;
            return true;
        }
        catch(Exception ex) when(IsUiaElementAccessException(ex)) { return false; }
    }

    public static bool TryGetElementHwnd(AutomationElement element, out IntPtr hwnd)
    {
        hwnd = IntPtr.Zero;
        try
        {
            if(element == null) return false;
            hwnd = new IntPtr(element.Current.NativeWindowHandle);
            return hwnd != IntPtr.Zero;
        }
        catch(Exception ex) when(IsUiaElementAccessException(ex)) { return false; }
    }

    public static string GetElementNameSafe(AutomationElement element)
    {
        try { return element?.Current.Name; }
        catch(Exception ex) when(IsUiaElementAccessException(ex)) { return "[Error getting name]"; }
    }

    public static ControlType GetControlTypeSafe(AutomationElement element)
    {
        try { return element?.Current.ControlType; }
        catch(Exception ex) when(IsUiaElementAccessException(ex)) { return null; }
    }

    public static bool IsUiaElementAccessException(Exception ex) => ex is ElementNotAvailableException || ex is InvalidOperationException || ex is COMException || ex is NullReferenceException;

    public static string ExtractAppNameFromTaskbarUiaName(string uiaName)
    {
        if(string.IsNullOrWhiteSpace(uiaName) || uiaName == "[Error getting name]") return string.Empty;
        try
        {
            Match match = NameExtractionRegex.Match(uiaName);
            if(match.Success && match.Groups.Count > 1 && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
                return match.Groups[1].Value.Trim();
            return uiaName.Trim();
        }
        catch(Exception ex)
        {
            return uiaName.Trim();
        }
    }
}