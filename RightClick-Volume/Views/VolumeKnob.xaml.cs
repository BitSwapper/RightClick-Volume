using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using RightClickVolume.Interfaces;
using RightClickVolume.Properties;
using RightClickVolume.ViewModels;


namespace RightClickVolume;

public partial class VolumeKnob : Window
{
    private VolumeKnobViewModel viewModel;
    private IAppAudioSession currentSession;

    bool isDraggingSlider = false;
    Thumb sliderThumb;
    bool isWindowInitializationComplete = false;

    DispatcherTimer peakMeterTimer;
    float peakLevelInternal;

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
        viewModel = new VolumeKnobViewModel();
        DataContext = viewModel;
        viewModel.RequestClose += Hide;

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

        bool sessionMuted = viewModel?.IsSessionMuted() ?? true;

        bool shouldTimerRun = Settings.Default.ShowPeakVolumeBar &&
                              this.IsVisible &&
                              currentSession != null &&
                              !sessionMuted;

        if(shouldTimerRun)
        {
            if(!peakMeterTimer.IsEnabled)
            {
                peakMeterTimer.Start();
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Peak meter timer STARTED for {viewModel.AppName} (Show: {Settings.Default.ShowPeakVolumeBar}, Visible: {this.IsVisible}, Muted: {sessionMuted}).");
            }
        }
        else
        {
            if(peakMeterTimer.IsEnabled)
            {
                peakMeterTimer.Stop();
                if(viewModel != null) viewModel.PeakLevel = 0;
                peakLevelInternal = 0;
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Peak meter timer STOPPED for {viewModel.AppName} (Show: {Settings.Default.ShowPeakVolumeBar}, Visible: {this.IsVisible}, Muted: {sessionMuted}).");
            }
            if(!Settings.Default.ShowPeakVolumeBar || sessionMuted || !this.IsVisible)
            {
                if(viewModel != null) viewModel.PeakLevel = 0;
                peakLevelInternal = 0;
            }
        }
    }

    public void ShowAt(double left, double top, IAppAudioSession session)
    {
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] VolumeKnob.ShowAt: Method called for session: {(session?.DisplayName ?? "NULL SESSION")}, PID: {(session?.ProcessId.ToString() ?? "N/A")}");

        currentSession = session;
        viewModel.InitializeSession(session);

        this.Left = left;
        this.Top = top;
        try
        {
            this.Show();
            this.Activate();
            Task.Run(async () =>
            {
                await Task.Delay(TOPMOST_RESET_INITIAL_DELAY);
                if(this.IsVisible) Dispatcher.Invoke(ResetTopmostState);
                await Task.Delay(TOPMOST_RESET_SECONDARY_DELAY);
                if(this.IsVisible) Dispatcher.Invoke(() => { if(this.IsVisible) { ResetTopmostState(); this.Activate(); } });
            });
            viewModel.PeakLevel = 0;
            peakLevelInternal = 0;
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
            try { this.DragMove(); } catch(InvalidOperationException) { }
        }
    }

    void VolumeKnob_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if(!isWindowInitializationComplete || currentSession == null || !viewModel.IsVolumeSliderEnabled) return;
        float newVolume = viewModel.Volume;
        if(e.Delta > 0) newVolume += MOUSE_WHEEL_VOLUME_STEP;
        else if(e.Delta < 0) newVolume -= MOUSE_WHEEL_VOLUME_STEP;
        viewModel.Volume = newVolume;
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
                if(this.IsVisible && Application.Current != null && !Application.Current.Dispatcher.HasShutdownStarted)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if(this.IsVisible && !this.IsMouseOver && !isDraggingSlider) Hide();
                    });
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion);
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
        isWindowInitializationComplete = true;
    }

    void VolumeKnob_Closed(object sender, EventArgs e)
    {
        Settings.Default.PropertyChanged -= Settings_PropertyChanged;
        if(peakMeterTimer != null)
        {
            peakMeterTimer.Stop();
            peakMeterTimer.Tick -= PeakMeterTimer_Tick;
            peakMeterTimer = null;
        }
        if(viewModel != null) viewModel.PeakLevel = 0;
        peakLevelInternal = 0;
        isWindowInitializationComplete = false;
        DetachSliderEvents();
        currentSession = null;
        if(viewModel != null) viewModel.RequestClose -= Hide;

        this.Deactivated -= VolumeKnob_Deactivated;
        this.Loaded -= VolumeKnob_Loaded;
        this.Closed -= VolumeKnob_Closed;
        this.KeyDown -= VolumeKnob_KeyDown;
        this.MouseLeave -= VolumeKnob_MouseLeave;

        DataContext = null;
        viewModel = null;
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] VolumeKnob_Closed: Cleanup complete.");
    }

    void AttachSliderEvents()
    {
        DetachSliderEvents();
        if(VolumeSlider != null && !VolumeSlider.IsLoaded) VolumeSlider.ApplyTemplate();
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

    void Thumb_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        Application.Current?.Dispatcher.InvokeAsync(() => { isDraggingSlider = false; });
    }


    static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        if(parent == null) return null;
        for(int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if(child is T tChild) return tChild;
            T childOfChild = FindVisualChild<T>(child);
            if(childOfChild != null) return childOfChild;
        }
        return null;
    }

    public new void Hide()
    {
        if(this.IsVisible)
        {
            this.Close();
        }
    }

    void PeakMeterTimer_Tick(object sender, EventArgs e)
    {
        if(currentSession != null && viewModel != null)
        {
            float rawPeak = 0f;
            try
            {
                rawPeak = currentSession.CurrentPeakValue;
            }
            catch(ObjectDisposedException)
            {
                if(viewModel != null) viewModel.PeakLevel = 0;
                peakLevelInternal = 0;
                if(peakMeterTimer != null && peakMeterTimer.IsEnabled) peakMeterTimer.Stop();
                currentSession = null;
                return;
            }
            catch(Exception)
            {
                if(viewModel != null) viewModel.PeakLevel = 0;
                peakLevelInternal = 0;
                return;
            }

            float curvedPeak = (float)Math.Pow(Math.Max(0, rawPeak), PeakCurvePower);
            float scaledPeak = curvedPeak * BasePeakMeterScaleFactor;
            float currentVolumeSettingFactor = viewModel.Volume / 100.0f;
            float targetPeakForUI = scaledPeak * currentVolumeSettingFactor;
            targetPeakForUI = Math.Clamp(targetPeakForUI, 0f, 100f);

            if(viewModel.IsSessionMuted())
                targetPeakForUI = 0;

            float currentDisplayedPeak = peakLevelInternal;
            float newSmoothedDisplayValue;
            float smoothingFactorToUse;

            if(targetPeakForUI > currentDisplayedPeak)
            {
                smoothingFactorToUse = (targetPeakForUI > currentDisplayedPeak * 2.0f) ? 1.0f : PeakAttackFactor;
            }
            else
            {
                smoothingFactorToUse = (currentDisplayedPeak > targetPeakForUI * 2.0f) ? PeakDecayFactor * 1.5f : PeakDecayFactor;
            }

            newSmoothedDisplayValue = currentDisplayedPeak + (targetPeakForUI - currentDisplayedPeak) * smoothingFactorToUse;

            if(smoothingFactorToUse == 1.0f)
                newSmoothedDisplayValue = targetPeakForUI;
            else if(smoothingFactorToUse == PeakAttackFactor || smoothingFactorToUse > 1.0f)
                newSmoothedDisplayValue = Math.Min(newSmoothedDisplayValue, targetPeakForUI);
            else
                newSmoothedDisplayValue = Math.Max(newSmoothedDisplayValue, targetPeakForUI);

            peakLevelInternal = newSmoothedDisplayValue;
            viewModel.PeakLevel = peakLevelInternal;
        }
        else
        {
            if(viewModel != null) viewModel.PeakLevel = 0;
            peakLevelInternal = 0;
            if(peakMeterTimer != null && peakMeterTimer.IsEnabled)
            {
                peakMeterTimer.Stop();
            }
        }
    }
}