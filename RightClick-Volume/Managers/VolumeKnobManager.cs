using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Threading;
using RightClickVolume.Interfaces;
using Application = System.Windows.Application;

namespace RightClickVolume.Managers;

internal class VolumeKnobManager : IVolumeKnobManager
{
    const int OffsetX = 140;
    const int OffsetY = -305;
    const int OffsetXWhenTopTooClose = 50;
    const int ScreenTopThreshold = 350;

    readonly Dictionary<IntPtr, VolumeKnob> activeKnobs = new();
    CancellationTokenSource cleanupCts;
    bool isDisposed = false;
    readonly object knobLock = new();

    public void ShowKnobForSession(int clickX, int clickY, IAppAudioSession session)
    {
        if(isDisposed || Application.Current == null || Application.Current.Dispatcher.HasShutdownStarted || session == null) return;

        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if(isDisposed || Application.Current == null || Application.Current.Dispatcher.HasShutdownStarted) return;

            IntPtr sessionKey = new IntPtr(session.ProcessId);
            HideAllKnobsInternal();

            System.Drawing.Point clickPoint = new System.Drawing.Point(clickX, clickY);
            Screen clickedScreen = Screen.FromPoint(clickPoint);
            System.Drawing.Rectangle screenBounds = clickedScreen.WorkingArea;

            int screenRelativeX = clickX - screenBounds.X;
            int screenRelativeY = clickY - screenBounds.Y;
            int yOffset = screenRelativeY < ScreenTopThreshold ? OffsetXWhenTopTooClose : OffsetY;

            int finalX = clickX + OffsetX;
            int finalY = clickY + yOffset;

            finalX = Math.Max(screenBounds.X, finalX);
            finalY = Math.Max(screenBounds.Y, finalY);

            VolumeKnob knob = null;
            try
            {
                knob = new VolumeKnob();
                knob.Closed += OnKnobClosed;

                lock(knobLock) activeKnobs[sessionKey] = knob;
                knob.ShowAt(finalX, finalY, session);
                AdjustKnobPositionIfNeeded(knob, finalX, finalY, screenBounds);
            }
            catch(Exception ex)
            {
                Debug.WriteLine($"Error showing knob for session {session.DisplayName}: {ex.Message}");
                if(knob != null)
                {
                    lock(knobLock) activeKnobs.Remove(sessionKey);
                    knob.Closed -= OnKnobClosed;
                    try { if(knob.IsLoaded) knob.Close(); } catch { }
                }
            }
        }, DispatcherPriority.Normal);
    }

    void AdjustKnobPositionIfNeeded(VolumeKnob knob, int finalX, int finalY, System.Drawing.Rectangle screenBounds)
    {
        if(knob.ActualWidth <= 0 || knob.ActualHeight <= 0)
        {
            knob.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if(knob.IsLoaded && (knob.ActualWidth > 0 && knob.ActualHeight > 0))
                {
                    int currentFinalX = (int)knob.Left;
                    int currentFinalY = (int)knob.Top;

                    if(currentFinalX + knob.ActualWidth > screenBounds.X + screenBounds.Width)
                    {
                        currentFinalX = screenBounds.X + screenBounds.Width - (int)knob.ActualWidth;
                        knob.Left = currentFinalX;
                    }

                    if(currentFinalY + knob.ActualHeight > screenBounds.Y + screenBounds.Height)
                    {
                        currentFinalY = screenBounds.Y + screenBounds.Height - (int)knob.ActualHeight;
                        knob.Top = currentFinalY;
                    }
                }
            }));
            return;
        }

        if(finalX + knob.ActualWidth > screenBounds.X + screenBounds.Width)
        {
            finalX = screenBounds.X + screenBounds.Width - (int)knob.ActualWidth;
            knob.Left = finalX;
        }

        if(finalY + knob.ActualHeight > screenBounds.Y + screenBounds.Height)
        {
            finalY = screenBounds.Y + screenBounds.Height - (int)knob.ActualHeight;
            knob.Top = finalY;
        }
    }

    void OnKnobClosed(object sender, EventArgs e)
    {
        if(sender is VolumeKnob closedKnob)
        {
            closedKnob.Closed -= OnKnobClosed;

            IntPtr keyToRemove = IntPtr.Zero;
            if(closedKnob.DataContext is ViewModels.VolumeKnobViewModel vm && vm.GetSessionDisplayName() != null)
            {
                lock(knobLock)
                {
                    foreach(var pair in activeKnobs)
                    {
                        if(pair.Value == closedKnob)
                        {
                            keyToRemove = pair.Key;
                            break;
                        }
                    }
                }
            }

            if(keyToRemove != IntPtr.Zero)
            {
                lock(knobLock)
                {
                    activeKnobs.Remove(keyToRemove);
                }
            }
        }
    }

    public void HideAllKnobs()
    {
        if(isDisposed || Application.Current == null) return;
        Application.Current.Dispatcher.InvokeAsync(HideAllKnobsInternal, DispatcherPriority.Normal);
    }

    void HideAllKnobsInternal()
    {
        if(isDisposed || Application.Current == null || Application.Current.Dispatcher.HasShutdownStarted) return;
        if(!Application.Current.Dispatcher.CheckAccess()) { Application.Current.Dispatcher.BeginInvoke(HideAllKnobsInternal, DispatcherPriority.Normal); return; }

        List<IntPtr> keysToProcess;
        lock(knobLock)
        {
            keysToProcess = activeKnobs.Keys.ToList();
        }

        foreach(var key in keysToProcess)
        {
            VolumeKnob knob;
            bool found;
            lock(knobLock)
            {
                found = activeKnobs.TryGetValue(key, out knob);
            }

            if(found && knob != null)
            {
                try
                {
                    if(knob.IsLoaded && knob.IsVisible)
                    {
                        knob.Hide();
                    }
                }
                catch(Exception ex)
                {
                    Debug.WriteLine($"Error hiding knob: {ex.Message}");
                    lock(knobLock) activeKnobs.Remove(key);
                }
            }
            else
            {
                lock(knobLock) activeKnobs.Remove(key);
            }
        }
    }

    public void StartCleanupTask()
    {
        if(isDisposed) return;
        StopCleanupTask();
        cleanupCts = new CancellationTokenSource();
        Task.Run(() => CleanupLoopAsync(cleanupCts.Token), cleanupCts.Token);
    }

    public void StopCleanupTask()
    {
        cleanupCts?.Cancel();
    }

    async Task CleanupLoopAsync(CancellationToken cancellationToken)
    {
        while(!cancellationToken.IsCancellationRequested)
        {
            try { await Task.Delay(TimeSpan.FromSeconds(60), cancellationToken); }
            catch(OperationCanceledException) { break; }
            if(cancellationToken.IsCancellationRequested) break;

            try
            {
                await Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    if(isDisposed || Application.Current == null || Application.Current.Dispatcher.HasShutdownStarted || cancellationToken.IsCancellationRequested) return;

                    List<IntPtr> keysToCheck;
                    lock(knobLock)
                    {
                        keysToCheck = activeKnobs.Keys.ToList();
                    }

                    foreach(IntPtr key in keysToCheck)
                    {
                        if(cancellationToken.IsCancellationRequested) break;
                        uint pid = (uint)key.ToInt64();
                        if(CheckProcessExists(pid)) continue;

                        VolumeKnob knob;
                        bool found;
                        lock(knobLock)
                        {
                            found = activeKnobs.TryGetValue(key, out knob);
                        }

                        if(found && knob != null)
                        {
                            try
                            {
                                if(knob.IsLoaded && knob.IsVisible)
                                {
                                    knob.Hide();
                                }
                            }
                            catch(Exception ex)
                            {
                                Debug.WriteLine($"Error hiding knob during cleanup: {ex.Message}");
                                lock(knobLock) activeKnobs.Remove(key);
                            }
                        }
                        else
                        {
                            lock(knobLock) activeKnobs.Remove(key);
                        }
                    }
                }, DispatcherPriority.Background, cancellationToken);
            }
            catch(OperationCanceledException) { break; }
            catch(Exception ex)
            {
                Debug.WriteLine($"Error in VolumeKnobManager cleanup loop: {ex.Message}");
                try { await Task.Delay(TimeSpan.FromSeconds(120), cancellationToken); }
                catch(OperationCanceledException) { break; }
            }
        }
    }

    bool CheckProcessExists(uint pid)
    {
        try
        {
            using Process process = Process.GetProcessById((int)pid);
            return process != null && !process.HasExited;
        }
        catch { return false; }
    }

    public void Dispose()
    {
        if(isDisposed) return;
        isDisposed = true;

        StopCleanupTask();

        if(Application.Current != null && !Application.Current.Dispatcher.HasShutdownStarted)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(HideAllKnobsInternal);
            }
            catch(Exception ex)
            {
                Debug.WriteLine($"Exception during Dispose->HideAllKnobsInternal: {ex.Message}");
            }
        }

        lock(knobLock)
        {
            activeKnobs.Clear();
        }

        cleanupCts?.Dispose();
        cleanupCts = null;
    }
}