// VolumeKnob.xaml.cs
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using RightClickVolume.Models;
using RightClickVolume.Properties;
using RightClickVolume.ViewModels;

namespace RightClickVolume;

public partial class VolumeKnob : Window
{
    VolumeKnobViewModel _viewModel;
    AppAudioSession _currentSession; // Keep a reference for timer logic not in VM

    bool isDraggingSlider = false;
    Thumb sliderThumb;
    bool isWindowInitializationComplete = false;

    DispatcherTimer peakMeterTimer;
    float _peakLevelInternal; // Internal value for smoothing, VM.PeakLevel is the final display value

    const float PeakAttackFactor = 0.9f;
    const float PeakDecayFactor = 0.80f;
    const float BasePeakMeterScaleFactor = 140.0f;
    const double PeakCurvePower = 0.65;
    const int PeakMeterTimerInterval = 3;

    const int TOPMOST_RESET_INITIAL_DELAY = 50;
    const int TOPMOST_RESET_SECONDARY_DELAY = 100;
    const int MouseLeaveHideDelay = 1000;
    const float MOUSE_WHEEL_VOLUME_STEP = 5.0f;

    public VolumeKnob()
    {
        InitializeComponent();
        _viewModel = new VolumeKnobViewModel();
        DataContext = _viewModel;
        _viewModel.RequestClose += Hide;

        isWindowInitializationComplete = false;

        this.Deactivated += VolumeKnob_Deactivated;
        this.Loaded += VolumeKnob_Loaded;
        this.Closed += VolumeKnob_Closed;
        this.KeyDown += VolumeKnob_KeyDown;
        this.MouseLeave += VolumeKnob_MouseLeave;

        peakMeterTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(PeakMeterTimerInterval)
        };
        peakMeterTimer.Tick += PeakMeterTimer_Tick;
        Settings.Default.PropertyChanged += Settings_PropertyChanged;
    }

    void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if(e.PropertyName == nameof(Settings.Default.ShowPeakVolumeBar))
        {
            UpdatePeakMeterTimerState();
        }
    }

    void UpdatePeakMeterTimerState()
    {
        if(peakMeterTimer == null) return;

        bool sessionMuted = _currentSession?.IsMuted ?? true; // If no session, consider muted for timer
        bool shouldTimerRun = Settings.Default.ShowPeakVolumeBar &&
                              this.IsVisible &&
                              _currentSession != null &&
                              !sessionMuted;

        if(shouldTimerRun)
        {
            if(!peakMeterTimer.IsEnabled)
            {
                peakMeterTimer.Start();
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Peak meter timer STARTED for {_viewModel.AppName} (Show: {Settings.Default.ShowPeakVolumeBar}, Visible: {this.IsVisible}, Muted: {sessionMuted}).");
            }
        }
        else
        {
            if(peakMeterTimer.IsEnabled)
            {
                peakMeterTimer.Stop();
                _viewModel.PeakLevel = 0;
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Peak meter timer STOPPED for {_viewModel.AppName} (Show: {Settings.Default.ShowPeakVolumeBar}, Visible: {this.IsVisible}, Muted: {sessionMuted}).");
            }
            if(!Settings.Default.ShowPeakVolumeBar || sessionMuted || !this.IsVisible)
            {
                _viewModel.PeakLevel = 0;
            }
        }
    }


    public void ShowAt(double left, double top, AppAudioSession session)
    {
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] VolumeKnob.ShowAt: Method called for session: {(session?.DisplayName ?? "NULL SESSION")}, PID: {(session?.ProcessId.ToString() ?? "N/A")}");
        _currentSession = session;
        _viewModel.InitializeSession(session);

        this.Left = left;
        this.Top = top;
        try
        {
            this.Show();
            this.Activate();
            Task.Run(async () =>
            {
                await Task.Delay(TOPMOST_RESET_INITIAL_DELAY); Dispatcher.Invoke(ResetTopmostState);
                await Task.Delay(TOPMOST_RESET_SECONDARY_DELAY); Dispatcher.Invoke(() => { ResetTopmostState(); this.Activate(); });
            });
            _viewModel.PeakLevel = 0;
            _peakLevelInternal = 0;
            UpdatePeakMeterTimerState();
        }
        catch(Exception ex)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] VolumeKnob.ShowAt: EXCEPTION: {ex.Message}");
            try { this.Close(); } catch { }
        }
    }

    void ResetTopmostState()
    {
        if(this.IsVisible)
        {
            this.Topmost = false;
            this.Topmost = true;
        }
    }

    void Window_MouseLeftButtonDown_Drag(object sender, MouseButtonEventArgs e)
    {
        if(e.ButtonState == MouseButtonState.Pressed)
        {
            if(e.Source is System.Windows.Controls.Button || e.Source is Thumb)
                return;

            try
            {
                this.DragMove();
            }
            catch(InvalidOperationException ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] DragMove() failed: {ex.Message}");
            }
        }
    }

    void VolumeKnob_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if(!isWindowInitializationComplete || _currentSession == null || !_viewModel.IsVolumeSliderEnabled)
            return;

        float newVolume = _viewModel.Volume;
        if(e.Delta > 0)
            newVolume += MOUSE_WHEEL_VOLUME_STEP;
        else if(e.Delta < 0)
            newVolume -= MOUSE_WHEEL_VOLUME_STEP;

        _viewModel.Volume = newVolume;
        e.Handled = true;
    }

    void VolumeKnob_KeyDown(object sender, KeyEventArgs e)
    {
        if(e.Key == Key.Escape) Hide();
    }

    void VolumeKnob_MouseLeave(object sender, MouseEventArgs e)
    {
        if(!isDraggingSlider && this.IsActive)
        {
            Task.Delay(MouseLeaveHideDelay).ContinueWith(t =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    if(this.IsVisible && !this.IsMouseOver && !isDraggingSlider) Hide();
                });
            });
        }
    }

    void VolumeKnob_Deactivated(object sender, EventArgs e)
    {
        if(!isDraggingSlider) Hide();
        else ResetTopmostState();
    }

    void VolumeKnob_Loaded(object sender, RoutedEventArgs e)
    {
        AttachSliderEvents();
        this.isWindowInitializationComplete = true;
    }

    void VolumeKnob_Closed(object sender, EventArgs e)
    {
        Settings.Default.PropertyChanged -= Settings_PropertyChanged;
        if(peakMeterTimer != null)
        {
            peakMeterTimer.Stop();
            peakMeterTimer.Tick -= PeakMeterTimer_Tick;
        }
        if(_viewModel != null) _viewModel.PeakLevel = 0;
        _peakLevelInternal = 0;
        this.isWindowInitializationComplete = false;
        DetachSliderEvents();
        _currentSession = null;
        if(_viewModel != null) _viewModel.RequestClose -= Hide;
        this.Deactivated -= VolumeKnob_Deactivated;
        this.Loaded -= VolumeKnob_Loaded;
        this.Closed -= VolumeKnob_Closed;
        this.KeyDown -= VolumeKnob_KeyDown;
        this.MouseLeave -= VolumeKnob_MouseLeave;
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] VolumeKnob_Closed: Cleanup complete.");
    }

    void AttachSliderEvents()
    {
        DetachSliderEvents();
        if(!VolumeSlider.IsLoaded) VolumeSlider.ApplyTemplate();
        sliderThumb = FindVisualChild<Thumb>(VolumeSlider);
        if(sliderThumb != null)
        {
            sliderThumb.DragStarted += Thumb_DragStarted;
            sliderThumb.DragCompleted += Thumb_DragCompleted;
        }
    }

    void DetachSliderEvents()
    {
        if(sliderThumb != null)
        {
            sliderThumb.DragStarted -= Thumb_DragStarted;
            sliderThumb.DragCompleted -= Thumb_DragCompleted;
            sliderThumb = null;
        }
    }

    void Thumb_DragStarted(object sender, DragStartedEventArgs e)
    {
        isDraggingSlider = true;
        ResetTopmostState();
    }

    void Thumb_DragCompleted(object sender, DragCompletedEventArgs e) => Application.Current?.Dispatcher.InvokeAsync(() => { isDraggingSlider = false; });

    static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        if(parent == null) return null;
        for(int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if(child != null && child is T tChild) return tChild;
            T childOfChild = FindVisualChild<T>(child);
            if(childOfChild != null) return childOfChild;
        }
        return null;
    }

    public new void Hide()
    {
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] VolumeKnob.Hide: Method called for App: {_viewModel?.AppName}. IsVisible: {this.IsVisible}");
        if(this.IsVisible)
        {
            if(peakMeterTimer != null && peakMeterTimer.IsEnabled)
            {
                peakMeterTimer.Stop();
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] VolumeKnob.Hide: Stopping peakMeterTimer prior to close.");
            }
            if(_viewModel != null) _viewModel.PeakLevel = 0;
            _peakLevelInternal = 0;
            try
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] VolumeKnob.Hide: Calling this.Close()");
                this.Close();
            }
            catch(Exception ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] VolumeKnob.Hide: EXCEPTION during this.Close(): {ex.Message}");
            }
        }
    }

    void PeakMeterTimer_Tick(object sender, EventArgs e)
    {
        if(_currentSession != null)
        {
            float rawPeak = 0f;
            try
            {
                rawPeak = _currentSession.CurrentPeakValue;
            }
            catch(ObjectDisposedException)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] PeakMeterTimer_Tick: Session '{_currentSession.DisplayName}' disposed. Stopping timer.");
                if(_viewModel != null) _viewModel.PeakLevel = 0;
                _peakLevelInternal = 0;
                if(peakMeterTimer.IsEnabled) peakMeterTimer.Stop();
                _currentSession = null;
                return;
            }
            catch(Exception ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] PeakMeterTimer_Tick: ERROR getting _currentSession.CurrentPeakValue for '{_currentSession.DisplayName}': {ex.Message}");
                if(_viewModel != null) _viewModel.PeakLevel = 0;
                _peakLevelInternal = 0;
                return;
            }

            float curvedPeak = (float)Math.Pow(Math.Max(0, rawPeak), PeakCurvePower);
            float scaledPeak = curvedPeak * BasePeakMeterScaleFactor;
            float currentVolumeSettingFactor = _viewModel.Volume / 100.0f; // Use VM Volume
            float targetPeakForUI = scaledPeak * currentVolumeSettingFactor;
            targetPeakForUI = Math.Clamp(targetPeakForUI, 0f, 100f);

            if(_currentSession.IsMuted)
                targetPeakForUI = 0;

            float currentDisplayedPeak = _peakLevelInternal;
            float newSmoothedDisplayValue;
            float smoothingFactorToUse;

            if(targetPeakForUI > currentDisplayedPeak)
            {
                if(targetPeakForUI > currentDisplayedPeak * 2.0f)
                    smoothingFactorToUse = 1.0f;
                else
                    smoothingFactorToUse = PeakAttackFactor;
            }
            else
            {
                if(currentDisplayedPeak > targetPeakForUI * 2.0f)
                    smoothingFactorToUse = PeakDecayFactor * 1.5f;
                else
                    smoothingFactorToUse = PeakDecayFactor;
            }

            newSmoothedDisplayValue = currentDisplayedPeak + (targetPeakForUI - currentDisplayedPeak) * smoothingFactorToUse;

            if(smoothingFactorToUse == 1.0f)
                newSmoothedDisplayValue = targetPeakForUI;
            else if(smoothingFactorToUse == PeakAttackFactor || smoothingFactorToUse > 1.0f)
                newSmoothedDisplayValue = Math.Min(newSmoothedDisplayValue, targetPeakForUI);
            else
                newSmoothedDisplayValue = Math.Max(newSmoothedDisplayValue, targetPeakForUI);

            _peakLevelInternal = newSmoothedDisplayValue;
            if(_viewModel != null) _viewModel.PeakLevel = _peakLevelInternal;
        }
        else
        {
            if(_viewModel != null) _viewModel.PeakLevel = 0;
            _peakLevelInternal = 0;
            if(peakMeterTimer.IsEnabled)
            {
                peakMeterTimer.Stop();
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] PeakMeterTimer_Tick: Session became null, stopping timer.");
            }
        }
    }
}