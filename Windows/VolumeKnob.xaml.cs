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

namespace RightClickVolume;

public partial class VolumeKnob : Window, INotifyPropertyChanged
{
    AppAudioSession session;
    float volume;
    string appName;
    string curVol = "0";
    bool isUpdatingFromCode;
    bool isDraggingSlider = false;
    Thumb sliderThumb;
    bool isWindowInitializationComplete = false;

    DispatcherTimer peakMeterTimer;
    float _peakLevel;

    const float PeakAttackFactor = 0.9f;
    const float PeakDecayFactor = 0.80f;
    const float BasePeakMeterScaleFactor = 140.0f;
    const double PeakCurvePower = 0.65;
    const int PeakMeterTimerInterval = 3;

    static readonly SolidColorBrush BG_Muted = new SolidColorBrush(Color.FromRgb(0x80, 0x30, 0x30));
    static readonly SolidColorBrush FG_Muted = Brushes.White;
    static readonly SolidColorBrush BG_UnMuted = Brushes.Transparent;
    static readonly SolidColorBrush FG_UnMuted = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));
    const int TOPMOST_RESET_INITIAL_DELAY = 50;
    const int TOPMOST_RESET_SECONDARY_DELAY = 100;
    const int MouseLeaveHideDelay = 1000;
    const float VolumeScaleFactor = 100.0f;
    const string FORMAT_VolumeDisplay = "F0";
    const string IconMuted = "🔇";
    const string IconUnMuted = "🔊";

    const float MOUSE_WHEEL_VOLUME_STEP = 5.0f;

    public event PropertyChangedEventHandler PropertyChanged;

    public float Volume
    {
        get => volume;
        set
        {
            if(System.Math.Abs(volume - value) < 0.001f && volume == value) return;

            float newVolumeClamped = Math.Clamp(value, 0f, 100f);
            if(System.Math.Abs(volume - newVolumeClamped) < 0.001f && volume == newVolumeClamped) return;

            volume = newVolumeClamped;
            OnPropertyChanged(nameof(Volume));
            CurVol = volume.ToString(FORMAT_VolumeDisplay);

            if(!isUpdatingFromCode && session != null)
            {
                if(session.IsMuted && volume > 0.001f)
                {
                    session.SetMute(false);
                    UpdateMuteStateVisuals();
                    UpdatePeakMeterTimerState();
                }
                session.SetVolume(volume / VolumeScaleFactor);
            }
        }
    }

    public string AppName
    {
        get => appName;
        set
        {
            if(appName == value) return;
            appName = value;
            OnPropertyChanged(nameof(AppName));
        }
    }

    public string CurVol
    {
        get => curVol;
        set
        {
            if(curVol == value) return;
            curVol = value;
            OnPropertyChanged(nameof(CurVol));
        }
    }

    public float PeakLevel
    {
        get => _peakLevel;
        set
        {
            if(Math.Abs(_peakLevel - value) > 0.0005f)
            {
                _peakLevel = value;
                OnPropertyChanged(nameof(PeakLevel));
            }
            else if(_peakLevel != value)
                _peakLevel = value;
        }
    }

    public VolumeKnob()
    {
        InitializeComponent();
        isWindowInitializationComplete = false;
        DataContext = this;

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
            UpdatePeakMeterTimerState();
    }

    void UpdatePeakMeterTimerState()
    {
        if(peakMeterTimer == null) return;
        bool shouldTimerRun = Settings.Default.ShowPeakVolumeBar &&
                              this.IsVisible &&
                              session != null &&
                              !session.IsMuted;
        if(shouldTimerRun)
        {
            if(!peakMeterTimer.IsEnabled)
            {
                peakMeterTimer.Start();
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Peak meter timer STARTED for {AppName} (Show: {Settings.Default.ShowPeakVolumeBar}, Visible: {this.IsVisible}, Muted: {session?.IsMuted}).");
            }
        }
        else
        {
            if(peakMeterTimer.IsEnabled)
            {
                peakMeterTimer.Stop();
                PeakLevel = 0;
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Peak meter timer STOPPED for {AppName} (Show: {Settings.Default.ShowPeakVolumeBar}, Visible: {this.IsVisible}, Muted: {session?.IsMuted}).");
            }
            if(!Settings.Default.ShowPeakVolumeBar || (session != null && session.IsMuted) || !this.IsVisible)
                PeakLevel = 0;
        }
    }

    public void ShowAt(double left, double top, AppAudioSession session)
    {
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] VolumeKnob.ShowAt: Method called for session: {(session?.DisplayName ?? "NULL SESSION")}, PID: {(session?.ProcessId.ToString() ?? "N/A")}");
        if(session == null)
            throw new ArgumentNullException(nameof(session));

        this.session = session;
        isUpdatingFromCode = true;
        AppName = session.DisplayName;
        Volume = session.Volume * VolumeScaleFactor;
        isUpdatingFromCode = false;
        UpdateMuteStateVisuals();
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
            PeakLevel = 0;
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
        if(!isWindowInitializationComplete || session == null || !VolumeSlider.IsEnabled)
            return;

        float newVolume = Volume;
        if(e.Delta > 0)
            newVolume += MOUSE_WHEEL_VOLUME_STEP;
        else if(e.Delta < 0)
            newVolume -= MOUSE_WHEEL_VOLUME_STEP;
        Volume = newVolume;
        e.Handled = true;
    }

    void VolumeKnob_KeyDown(object sender, KeyEventArgs e)
    {
        if(e.Key == Key.Escape) Hide();
    }

    void VolumeKnob_MouseLeave(object sender, MouseEventArgs e)
    {
        if(!isDraggingSlider && this.IsActive)
            Task.Delay(MouseLeaveHideDelay).ContinueWith(t =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    if(this.IsVisible && !this.IsMouseOver && !isDraggingSlider) Hide();
                });
            });
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
        PeakLevel = 0;
        this.isWindowInitializationComplete = false;
        DetachSliderEvents();
        session = null;
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

    void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if(!this.isWindowInitializationComplete) return;
    }

    void MuteButton_Click(object sender, RoutedEventArgs e)
    {
        if(session != null)
        {
            session.SetMute(!session.IsMuted);
            UpdateMuteStateVisuals();
            UpdatePeakMeterTimerState();
            if(session.IsMuted)
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] MuteButton_Click: Session '{session.DisplayName}' MUTED.");
            else
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] MuteButton_Click: Session '{session.DisplayName}' UNMUTED.");
        }
    }

    void CloseButton_Click(object sender, RoutedEventArgs e) => Hide();

    void UpdateMuteStateVisuals()
    {
        if(session == null || MuteButton == null || VolumeSlider == null) return;
        try
        {
            bool isMuted = session.IsMuted;
            MuteButton.Content = isMuted ? IconMuted : IconUnMuted;
            MuteButton.Background = isMuted ? BG_Muted : BG_UnMuted;
            MuteButton.Foreground = isMuted ? FG_Muted : FG_UnMuted;
            VolumeSlider.IsEnabled = !isMuted;
        }
        catch(Exception ex)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] UpdateMuteStateVisuals: EXCEPTION: {ex.Message}");
        }
    }

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

    protected void OnPropertyChanged(string propertyName)
    {
        if(propertyName == nameof(PeakLevel))
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] OnPropertyChanged Fired for: {propertyName} (New Value: {_peakLevel:F4}) on App: {this.AppName}");
        else if(propertyName == nameof(Volume))
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] OnPropertyChanged Fired for: {propertyName} (New Value: {this.volume:F2}) on App: {this.AppName}");
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public new void Hide()
    {
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] VolumeKnob.Hide: Method called for App: {this.AppName}. IsVisible: {this.IsVisible}");
        if(this.IsVisible)
        {
            if(peakMeterTimer != null && peakMeterTimer.IsEnabled)
            {
                peakMeterTimer.Stop();
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] VolumeKnob.Hide: Stopping peakMeterTimer prior to close.");
            }
            PeakLevel = 0;
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
        if(session != null)
        {
            float rawPeak = 0f;
            try
            {
                rawPeak = session.CurrentPeakValue;
            }
            catch(ObjectDisposedException)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] PeakMeterTimer_Tick: Session '{session.DisplayName}' disposed. Stopping timer.");
                PeakLevel = 0;
                if(peakMeterTimer.IsEnabled) peakMeterTimer.Stop();
                session = null;
                return;
            }
            catch(Exception ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] PeakMeterTimer_Tick: ERROR getting session.CurrentPeakValue for '{session.DisplayName}': {ex.Message}");
                PeakLevel = 0;
                return;
            }

            float curvedPeak = (float)Math.Pow(Math.Max(0, rawPeak), PeakCurvePower);
            float scaledPeak = curvedPeak * BasePeakMeterScaleFactor;
            float currentVolumeSettingFactor = this.Volume / 100.0f;
            float targetPeakForUI = scaledPeak * currentVolumeSettingFactor;
            targetPeakForUI = Math.Clamp(targetPeakForUI, 0f, 100f);

            if(session.IsMuted)
                targetPeakForUI = 0;

            float currentDisplayedPeak = _peakLevel;
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

            PeakLevel = newSmoothedDisplayValue;
        }
        else
        {
            PeakLevel = 0;
            if(peakMeterTimer.IsEnabled)
            {
                peakMeterTimer.Stop();
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] PeakMeterTimer_Tick: Session became null, stopping timer.");
            }
        }
    }
}