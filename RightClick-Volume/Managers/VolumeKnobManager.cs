using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Threading;
using RightClickVolume.Models;
using Application = System.Windows.Application;

namespace RightClickVolume.Managers;

internal class VolumeKnobManager : IDisposable
{
    const int OffsetX = 140;
    const int OffsetY = -305;
    const int OffsetXWhenTopTooClose = 50;
    const int ScreenTopThreshold = 350;

    readonly Dictionary<IntPtr, VolumeKnob> activeKnobs = new();
    CancellationTokenSource cleanupCts;
    bool isDisposed = false;
    readonly object _lock = new();

    public void ShowKnobForSession(int clickX, int clickY, AppAudioSession session)
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
                knob = new();
                knob.Closed += OnKnobClosed;

                lock(_lock) activeKnobs[sessionKey] = knob;
                knob.ShowAt(finalX, finalY, session);
                AdjustKnobPositionIfNeeded(knob, finalX, finalY, screenBounds);
            }
            catch(Exception ex)
            {
                if(knob != null)
                {
                    lock(_lock) activeKnobs.Remove(sessionKey);
                    knob.Closed -= OnKnobClosed;
                    try { if(knob.IsLoaded) knob.Close(); } catch { }
                }
            }
        }, DispatcherPriority.Normal);
    }

    void AdjustKnobPositionIfNeeded(VolumeKnob knob, int finalX, int finalY, System.Drawing.Rectangle screenBounds)
    {
        if(knob.ActualWidth <= 0 || knob.ActualHeight <= 0) return;

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
        if(sender is VolumeKnob knob && knob.DataContext is AppAudioSession session)
            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                lock(_lock) activeKnobs.Remove(new IntPtr(session.ProcessId));
            }, DispatcherPriority.Input);

        if(sender is VolumeKnob closedKnob) closedKnob.Closed -= OnKnobClosed;
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

        List<IntPtr> keysToRemove;
        lock(_lock) keysToRemove = activeKnobs.Keys.ToList();

        if(keysToRemove.Count == 0) return;

        foreach(var key in keysToRemove)
        {
            VolumeKnob knob;
            lock(_lock) activeKnobs.TryGetValue(key, out knob);

            if(knob != null)
            {
                try { if(knob.IsLoaded && knob.IsVisible) knob.Hide(); }
                catch { }
                finally { lock(_lock) activeKnobs.Remove(key); }
            }
            else lock(_lock) activeKnobs.Remove(key);
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
        cleanupCts?.Dispose();
        cleanupCts = null;
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
                    lock(_lock) keysToCheck = activeKnobs.Keys.ToList();
                    if(keysToCheck.Count == 0) return;

                    foreach(IntPtr key in keysToCheck)
                    {
                        if(cancellationToken.IsCancellationRequested) break;
                        uint pid = (uint)key.ToInt64();
                        if(CheckProcessExists(pid)) continue;

                        VolumeKnob knob;
                        lock(_lock) activeKnobs.TryGetValue(key, out knob);

                        if(knob != null)
                        {
                            try { if(knob.IsLoaded && knob.IsVisible) knob.Hide(); }
                            catch { }
                            finally { lock(_lock) activeKnobs.Remove(key); }
                        }
                        else lock(_lock) activeKnobs.Remove(key);
                    }
                }, DispatcherPriority.Background, cancellationToken);
            }
            catch(OperationCanceledException) { break; }
            catch(Exception ex)
            {
                try { await Task.Delay(TimeSpan.FromSeconds(120), cancellationToken); }
                catch(OperationCanceledException) { break; }
            }
        }
    }

    bool CheckProcessExists(uint pid)
    {
        try { using Process process = Process.GetProcessById((int)pid); return !process.HasExited; }
        catch(ArgumentException) { return false; }
        catch(InvalidOperationException) { return false; }
        catch(System.ComponentModel.Win32Exception ex) when(ex.NativeErrorCode == 5) { return true; }
        catch(Exception) { return false; }
    }

    public void Dispose() => Dispose(true);

    protected virtual void Dispose(bool disposing)
    {
        if(isDisposed) return;

        if(disposing)
        {
            StopCleanupTask();
            HideAllKnobs();
            lock(_lock) activeKnobs.Clear();
        }
        isDisposed = true;
    }
}