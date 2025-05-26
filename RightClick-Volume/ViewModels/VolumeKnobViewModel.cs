using System;
using System.Diagnostics;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RightClickVolume.Interfaces;

namespace RightClickVolume.ViewModels;

public partial class VolumeKnobViewModel : ObservableObject
{
    private IAppAudioSession session;
    private bool isUpdatingVolumeFromCode;

    const float VolumeScaleFactor = 100.0f;
    const string FORMAT_VolumeDisplay = "F0";
    const string IconMuted = "🔇";
    const string IconUnMuted = "🔊";

    private static readonly SolidColorBrush s_bgMuted;
    private static readonly SolidColorBrush s_fgMuted;
    private static readonly SolidColorBrush s_bgUnMuted;
    private static readonly SolidColorBrush s_fgUnMuted;

    static VolumeKnobViewModel()
    {
        s_bgMuted = new SolidColorBrush(Color.FromRgb(0x80, 0x30, 0x30));
        if(s_bgMuted.CanFreeze) s_bgMuted.Freeze();

        s_fgMuted = (SolidColorBrush)Brushes.White;

        s_bgUnMuted = (SolidColorBrush)Brushes.Transparent;

        s_fgUnMuted = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));
        if(s_fgUnMuted.CanFreeze) s_fgUnMuted.Freeze();
    }

    [ObservableProperty]
    private float _volume;

    [ObservableProperty]
    private string _appName;

    [ObservableProperty]
    private string _curVol;

    [ObservableProperty]
    private float _peakLevel;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private string _muteButtonContent;

    [ObservableProperty]
    private Brush _muteButtonBackground;

    [ObservableProperty]
    private Brush _muteButtonForeground;

    [ObservableProperty]
    private bool _isVolumeSliderEnabled;

    public event Action RequestClose;

    public VolumeKnobViewModel()
    {
        MuteButtonContent = IconUnMuted;
        MuteButtonBackground = s_bgUnMuted;
        MuteButtonForeground = s_fgUnMuted;
        IsVolumeSliderEnabled = true;
    }

    public void InitializeSession(IAppAudioSession session)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        AppName = this.session.DisplayName;

        isUpdatingVolumeFromCode = true;
        Volume = this.session.Volume * VolumeScaleFactor;
        isUpdatingVolumeFromCode = false;

        UpdateMuteState();
    }

    partial void OnVolumeChanged(float value)
    {
        float newVolumeClamped = Math.Clamp(value, 0f, 100f);

        if(Math.Abs(value - newVolumeClamped) > 0.0001f)
        {
            this.Volume = newVolumeClamped;
            return;
        }

        CurVol = value.ToString(FORMAT_VolumeDisplay);

        if(!isUpdatingVolumeFromCode && session != null)
        {
            if(session.IsMuted && value > 0.001f)
            {
                session.SetMute(false);
                UpdateMuteState();
            }
            session.SetVolume(value / VolumeScaleFactor);
        }
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] OnVolumeChanged: Volume is now {value:F2} on App: {this.AppName}");
    }

    partial void OnPeakLevelChanged(float value)
    {
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] OnPropertyChanged Fired for: {nameof(PeakLevel)} (New Value: {_peakLevel:F4}) on App: {this.AppName}");
    }


    [RelayCommand]
    private void Mute()
    {
        if(session != null)
        {
            session.SetMute(!session.IsMuted);
            UpdateMuteState();
            if(session.IsMuted)
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] MuteCommand: Session '{session.DisplayName}' MUTED.");
            else
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] MuteCommand: Session '{session.DisplayName}' UNMUTED.");
        }
    }

    [RelayCommand]
    private void Close() => RequestClose?.Invoke();

    public void UpdateMuteState()
    {
        if(session == null) return;
        IsMuted = session.IsMuted;
        MuteButtonContent = IsMuted ? IconMuted : IconUnMuted;
        MuteButtonBackground = IsMuted ? s_bgMuted : s_bgUnMuted;
        MuteButtonForeground = IsMuted ? s_fgMuted : s_fgUnMuted;
        IsVolumeSliderEnabled = !IsMuted;
    }

    public bool IsSessionMuted() => session?.IsMuted ?? false;
    public string GetSessionDisplayName() => session?.DisplayName;
}