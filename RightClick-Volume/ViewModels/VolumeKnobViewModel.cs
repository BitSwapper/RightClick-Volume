using System;
using System.Diagnostics;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RightClickVolume.Models;

namespace RightClickVolume.ViewModels;

public partial class VolumeKnobViewModel : ObservableObject
{
    AppAudioSession _session;
    bool _isUpdatingVolumeFromCode;

    const float VolumeScaleFactor = 100.0f;
    const string FORMAT_VolumeDisplay = "F0";
    const string IconMuted = "🔇";
    const string IconUnMuted = "🔊";
    static readonly SolidColorBrush BG_Muted = new SolidColorBrush(Color.FromRgb(0x80, 0x30, 0x30));
    static readonly SolidColorBrush FG_Muted = Brushes.White;
    static readonly SolidColorBrush BG_UnMuted = Brushes.Transparent;
    static readonly SolidColorBrush FG_UnMuted = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));

    [ObservableProperty]
    float _volume;

    [ObservableProperty]
    string _appName;

    [ObservableProperty]
    string _curVol;

    [ObservableProperty]
    float _peakLevel;

    [ObservableProperty]
    bool _isMuted;

    [ObservableProperty]
    string _muteButtonContent;

    [ObservableProperty]
    Brush _muteButtonBackground;

    [ObservableProperty]
    Brush _muteButtonForeground;

    [ObservableProperty]
    bool _isVolumeSliderEnabled;

    public event Action RequestClose;

    public VolumeKnobViewModel()
    {
        MuteButtonContent = IconUnMuted;
        MuteButtonBackground = BG_UnMuted;
        MuteButtonForeground = FG_UnMuted;
        IsVolumeSliderEnabled = true;
    }

    public void InitializeSession(AppAudioSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        AppName = _session.DisplayName;

        _isUpdatingVolumeFromCode = true;
        Volume = _session.Volume * VolumeScaleFactor;
        _isUpdatingVolumeFromCode = false;

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

        if(!_isUpdatingVolumeFromCode && _session != null)
        {
            if(_session.IsMuted && value > 0.001f)
            {
                _session.SetMute(false);
                UpdateMuteState();
            }
            _session.SetVolume(value / VolumeScaleFactor);
        }

        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] OnVolumeChanged: Volume is now {value:F2} on App: {this.AppName}");
    }

    partial void OnPeakLevelChanged(float value)
    {
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] OnPropertyChanged Fired for: {nameof(PeakLevel)} (New Value: {value:F4}) on App: {this.AppName}");
    }


    [RelayCommand]
    void Mute()
    {
        if(_session != null)
        {
            _session.SetMute(!_session.IsMuted);
            UpdateMuteState();
            if(_session.IsMuted)
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] MuteCommand: Session '{_session.DisplayName}' MUTED.");
            else
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] MuteCommand: Session '{_session.DisplayName}' UNMUTED.");
        }
    }

    [RelayCommand]
    void Close() => RequestClose?.Invoke();

    public void UpdateMuteState()
    {
        if(_session == null) return;
        IsMuted = _session.IsMuted;
        MuteButtonContent = IsMuted ? IconMuted : IconUnMuted;
        MuteButtonBackground = IsMuted ? BG_Muted : BG_UnMuted;
        MuteButtonForeground = IsMuted ? FG_Muted : FG_UnMuted;
        IsVolumeSliderEnabled = !IsMuted;
    }

    public bool IsSessionMuted() => _session?.IsMuted ?? false;
    public string GetSessionDisplayName() => _session?.DisplayName;
}