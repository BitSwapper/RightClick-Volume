using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
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

    public event PropertyChangedEventHandler PropertyChanged;

    public float Volume
    {
        get => volume;
        set
        {
            if(volume == value) return;
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

    public VolumeKnob()
    {
        InitializeComponent();
        DataContext = this;

        this.Deactivated += VolumeKnob_Deactivated;
        this.Loaded += VolumeKnob_Loaded;
        this.Closed += VolumeKnob_Closed;
        this.KeyDown += VolumeKnob_KeyDown;
        this.MouseLeave += VolumeKnob_MouseLeave;
    }

    public void ShowAt(double left, double top, AppAudioSession session)
    {
        if(session == null) throw new ArgumentNullException(nameof(session));
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
                await Task.Delay(TOPMOST_RESET_INITIAL_DELAY);
                Dispatcher.Invoke(ResetTopmostState);
                await Task.Delay(TOPMOST_RESET_SECONDARY_DELAY);
                Dispatcher.Invoke(() =>
                {
                    ResetTopmostState();
                    this.Activate();
                });
            });
        }
        catch
        {
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
        if(e.Key == Key.Escape)
            Hide();
    }

    void VolumeKnob_MouseLeave(object sender, MouseEventArgs e)
    {
        if(!isDraggingSlider && this.IsActive)
            Task.Delay(MouseLeaveHideDelay).ContinueWith(t =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    if(this.IsVisible && !this.IsMouseOver && !isDraggingSlider)
                        Hide();
                });
            });
    }

    void VolumeKnob_Deactivated(object sender, EventArgs e)
    {
        if(!isDraggingSlider)
            Hide();
        else
            ResetTopmostState();
    }

    void VolumeKnob_Loaded(object sender, RoutedEventArgs e) => AttachSliderEvents();

    void VolumeKnob_Closed(object sender, EventArgs e)
    {
        DetachSliderEvents();
        session = null;
        this.Deactivated -= VolumeKnob_Deactivated;
        this.Loaded -= VolumeKnob_Loaded;
        this.Closed -= VolumeKnob_Closed;
        this.KeyDown -= VolumeKnob_KeyDown;
        this.MouseLeave -= VolumeKnob_MouseLeave;
    }

    void AttachSliderEvents()
    {
        DetachSliderEvents();
        if(!VolumeSlider.IsLoaded)
            VolumeSlider.ApplyTemplate();

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
        if(!isUpdatingFromCode && session != null)
        {
            float newVolume = (float)e.NewValue / VolumeScaleFactor;
            if(session.IsMuted)
            {
                session.SetMute(!session.IsMuted);
                UpdateMuteStateVisuals();
            }
            session.SetVolume(newVolume);

            isUpdatingFromCode = true;
            Volume = (float)e.NewValue;
            isUpdatingFromCode = false;
        }
    }

    void MuteButton_Click(object sender, RoutedEventArgs e)
    {
        if(session != null)
        {
            session.SetMute(!session.IsMuted);
            UpdateMuteStateVisuals();
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

            if(isMuted)
            {
                MuteButton.Background = BG_Muted;
                MuteButton.Foreground = FG_Muted;
            }
            else
            {
                MuteButton.Background = BG_UnMuted;
                MuteButton.Foreground = FG_UnMuted;
            }

            VolumeSlider.IsEnabled = !isMuted;
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

    protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public new void Hide()
    {
        if(this.IsVisible)
            try { this.Close(); }
            catch { }
    }
}