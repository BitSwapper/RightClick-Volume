using System.Collections.Specialized;

namespace RightClickVolume.Interfaces;

public interface ISettingsService
{
    bool LaunchOnStartup { get; set; }
    bool ShowPeakVolumeBar { get; set; }
    bool Hotkey_Ctrl { get; set; }
    bool Hotkey_Alt { get; set; }
    bool Hotkey_Shift { get; set; }
    bool Hotkey_Win { get; set; }
    StringCollection ManualMappings { get; set; }
    void Save();
    event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

}