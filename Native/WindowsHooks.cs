﻿using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RightClickVolume.Native;

public class WindowsHooks
{
    #region Win32 API Declarations

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    public static extern IntPtr WindowFromPoint(POINT point);

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT lpPoint);

    const int WH_MOUSE_LL = 14;
    const int WM_RBUTTONDOWN = 0x0204;
    const int WM_RBUTTONUP = 0x0205;

    delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    #endregion

    #region Hook Implementation

    IntPtr mouseHookHandle = IntPtr.Zero;
    HookProc mouseProc;
    public event EventHandler<MouseHookEventArgs> RightMouseClick;

    public WindowsHooks() => mouseProc = MouseHookCallback;


    public void InstallMouseHook()
    {
        if(mouseHookHandle == IntPtr.Zero)
        {
            mouseProc = MouseHookCallback;
            mouseHookHandle = SetWindowsHookEx(WH_MOUSE_LL, mouseProc,
                GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName), 0);

            if(mouseHookHandle == IntPtr.Zero)
            {
                int errorCode = Marshal.GetLastWin32Error();
                Debug.WriteLine($"Failed to install mouse hook. Error code: {errorCode}");
            }
        }
    }


    public void UninstallMouseHook()
    {
        if(mouseHookHandle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(mouseHookHandle);
            mouseHookHandle = IntPtr.Zero;
        }
    }


    IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if(nCode >= 0)
        {
            MSLLHOOKSTRUCT hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));

            if(wParam == (IntPtr)WM_RBUTTONUP)
            {
                POINT cursorPos = hookStruct.pt;
                IntPtr windowUnderCursor = WindowFromPoint(cursorPos);

                RightMouseClick?.Invoke(this, new MouseHookEventArgs
                {
                    X = cursorPos.X,
                    Y = cursorPos.Y,
                    WindowHandle = windowUnderCursor
                });
            }
        }

        return CallNextHookEx(mouseHookHandle, nCode, wParam, lParam);
    }

    #endregion

    #region Native Structures

    [StructLayout(LayoutKind.Sequential)]
    struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    #endregion
}


public class MouseHookEventArgs : EventArgs
{
    public int X { get; set; }
    public int Y { get; set; }
    public IntPtr WindowHandle { get; set; }
}