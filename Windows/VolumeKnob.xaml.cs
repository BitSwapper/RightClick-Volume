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
    const float BasePeakMeterScaleFactor = 150.0f;

    DispatcherTimer peakMeterTimer;
    float _peakLevel;

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
    const float PeakMeterScaleFactor = 100.0f;
    const double PeakCurvePower = 0.75;

    public event PropertyChangedEventHandler PropertyChanged;

    public float Volume
    {
        get => volume;
        set
        {
            if(System.Math.Abs(volume - value) < 0.001f) return;
            volume = value;
            OnPropertyChanged(nameof(Volume));
            CurVol = volume.ToString(FORMAT_VolumeDisplay);
            if(!isUpdatingFromCode && session != null)
            {
                if(session.IsMuted)
                {
                    session.SetMute(!session.IsMuted);
                    UpdateMuteStateVisuals();
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
            if(Math.Abs(_peakLevel - value) > 0.001f)
            {
                _peakLevel = value;
                OnPropertyChanged(nameof(PeakLevel));
            }
        }
    }

    public VolumeKnob()
    {
        InitializeComponent();
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] VolumeKnob: Constructor called.");
        isWindowInitializationComplete = false;
        DataContext = this;

        this.Deactivated += VolumeKnob_Deactivated;
        this.Loaded += VolumeKnob_Loaded;
        this.Closed += VolumeKnob_Closed;
        this.KeyDown += VolumeKnob_KeyDown;
        this.MouseLeave += VolumeKnob_MouseLeave;

        peakMeterTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(5)
        };
        peakMeterTimer.Tick += PeakMeterTimer_Tick;
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] VolumeKnob: peakMeterTimer Initialized. Interval: {peakMeterTimer.Interval.TotalMilliseconds}ms");
    }




    public void ShowAt(double left, double top, AppAudioSession session)
    {
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] VolumeKnob.ShowAt: Method called for session: {(session?.DisplayName ?? "NULL SESSION")}, PID: {(session?.ProcessId.ToString() ?? "N/A")}");
        if(session == null)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] VolumeKnob.ShowAt: Session is NULL. Aborting show and throwing ArgumentNullException.");
            throw new ArgumentNullException(nameof(session));
        }
        this.session = session;

        isUpdatingFromCode = true;
        AppName = session.DisplayName;
        Volume = session.Volume * VolumeScaleFactor;
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] VolumeKnob.ShowAt: AppName='{AppName}', Initial UI Volume={this.Volume} (Session Vol: {session.Volume:F2})");
        isUpdatingFromCode = false;

        UpdateMuteStateVisuals();

        this.Left = left;
        this.Top = top;

        try
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] VolumeKnob.ShowAt: Calling this.Show() and this.Activate()");
            this.Show();
            this.Activate();

            Task.Run(async () =>
            {
                await Task.Delay(TOPMOST_RESET_INITIAL_DELAY); Dispatcher.Invoke(ResetTopmostState);
                await Task.Delay(TOPMOST_RESET_SECONDARY_DELAY); Dispatcher.Invoke(() => { ResetTopmostState(); this.Activate(); });
            });

            if(peakMeterTimer != null)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] VolumeKnob.ShowAt: Starting peakMeterTimer. Current PeakLevel (before reset): {PeakLevel:F2}");
                PeakLevel = 0;
                peakMeterTimer.Start();
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] VolumeKnob.ShowAt: peakMeterTimer STARTED. IsEnabled: {peakMeterTimer.IsEnabled}");
            }
            else
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] VolumeKnob.ShowAt: peakMeterTimer is NULL! Cannot start.");
            }
        }
        catch(Exception ex)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] VolumeKnob.ShowAt: EXCEPTION during Show/Activate or timer start: {ex.Message}");
            try { this.Close(); } catch { }
        }
    }

    void ResetTopmostState()
    {
        this.Topmost = false;
        this.Topmost = true;
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
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] VolumeKnob_Loaded: Fired.");
        AttachSliderEvents();
        this.isWindowInitializationComplete = true;
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] VolumeKnob_Loaded: isWindowInitializationComplete = true.");
    }

    void VolumeKnob_Closed(object sender, EventArgs e)
    {
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] VolumeKnob_Closed: Fired for App: {this.AppName}");
        if(peakMeterTimer != null)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] VolumeKnob_Closed: Stopping peakMeterTimer. WasEnabled: {peakMeterTimer.IsEnabled}");
            peakMeterTimer.Stop();
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

        if(!isUpdatingFromCode && session != null)
        {
        }
    }

    void MuteButton_Click(object sender, RoutedEventArgs e)
    {
        if(session != null)
        {
            session.SetMute(!session.IsMuted);
            UpdateMuteStateVisuals();
            if(session.IsMuted)
            {
                PeakLevel = 0;
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] MuteButton_Click: Session '{session.DisplayName}' MUTED. PeakLevel set to 0.");
            }
            else
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] MuteButton_Click: Session '{session.DisplayName}' UNMUTED.");
            }
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
            if(PeakVolumeMeter != null) PeakVolumeMeter.IsEnabled = !isMuted;
        }
        catch { }
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
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] OnPropertyChanged Fired for: {propertyName} (New Value: {_peakLevel:F4}) on App: {this.AppName}");
        }
        else if(propertyName == nameof(Volume))
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] OnPropertyChanged Fired for: {propertyName} (New Value: {this.volume:F2}) on App: {this.AppName}");
        }
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public new void Hide()
    {
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] VolumeKnob.Hide: Method called for App: {this.AppName}. IsVisible: {this.IsVisible}");
        if(this.IsVisible)
        {
            if(peakMeterTimer != null && peakMeterTimer.IsEnabled)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] VolumeKnob.Hide: Stopping peakMeterTimer.");
                peakMeterTimer.Stop();
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
        if(session != null && this.IsVisible)
        {
            float rawPeak = 0f;
            try
            {
                rawPeak = session.CurrentPeakValue;
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

            float finalPeakForUI = scaledPeak * currentVolumeSettingFactor;

            finalPeakForUI = Math.Clamp(finalPeakForUI, 0f, 100f);


            if(session.IsMuted)
            {
                finalPeakForUI = 0;
            }

            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] PeakMeterTick for '{session.DisplayName}': Vis={this.IsVisible}, Muted={session.IsMuted}, RawPeak={rawPeak:F5}, CurvedP={curvedPeak:F5}, ScaledP={scaledPeak:F2}, VolFactor={currentVolumeSettingFactor:F2}, FinalUI={finalPeakForUI:F2}, OldPeakProp={_peakLevel:F2}");

            PeakLevel = finalPeakForUI;
        }
        else
        {
            string reason = "Unknown";
            if(session == null) reason = "Session is null";
            else if(!this.IsVisible) reason = "Window not visible";

            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] PeakMeterTimer_Tick: Conditions not met ({reason}). Setting PeakLevel to 0.");
            PeakLevel = 0;
        }
    }
}