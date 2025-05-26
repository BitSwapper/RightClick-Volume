using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using RightClickVolume.Interfaces;
using RightClickVolume.Properties;

namespace RightClickVolume.Services;

public class SettingsService : ISettingsService, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public bool LaunchOnStartup
    {
        get => Settings.Default.LaunchOnStartup;
        set
        {
            if(Settings.Default.LaunchOnStartup != value)
            {
                Settings.Default.LaunchOnStartup = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ShowPeakVolumeBar
    {
        get => Settings.Default.ShowPeakVolumeBar;
        set
        {
            if(Settings.Default.ShowPeakVolumeBar != value)
            {
                Settings.Default.ShowPeakVolumeBar = value;
                OnPropertyChanged();
            }
        }
    }
    public bool Hotkey_Ctrl
    {
        get => Settings.Default.Hotkey_Ctrl;
        set
        {
            if(Settings.Default.Hotkey_Ctrl != value)
            {
                Settings.Default.Hotkey_Ctrl = value;
                OnPropertyChanged();
            }
        }
    }

    public bool Hotkey_Alt
    {
        get => Settings.Default.Hotkey_Alt;
        set
        {
            if(Settings.Default.Hotkey_Alt != value)
            {
                Settings.Default.Hotkey_Alt = value;
                OnPropertyChanged();
            }
        }
    }

    public bool Hotkey_Shift
    {
        get => Settings.Default.Hotkey_Shift;
        set
        {
            if(Settings.Default.Hotkey_Shift != value)
            {
                Settings.Default.Hotkey_Shift = value;
                OnPropertyChanged();
            }
        }
    }

    public bool Hotkey_Win
    {
        get => Settings.Default.Hotkey_Win;
        set
        {
            if(Settings.Default.Hotkey_Win != value)
            {
                Settings.Default.Hotkey_Win = value;
                OnPropertyChanged();
            }
        }
    }

    public StringCollection ManualMappings
    {
        get => Settings.Default.ManualMappings;
        set
        {
            if(Settings.Default.ManualMappings != value)
            {
                Settings.Default.ManualMappings = value;
                OnPropertyChanged();
            }
        }
    }

    public void Save() => Settings.Default.Save();

    public SettingsService() => Settings.Default.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
}