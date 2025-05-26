// SettingsViewModel.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RightClickVolume.Properties;

namespace RightClickVolume.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    const char UIA_PROCESS_SEPARATOR = '|';
    const char PROCESS_LIST_SEPARATOR = ';';

    [ObservableProperty]
    ObservableCollection<MappingEntry> _mappings;

    [ObservableProperty]
    MappingEntry _selectedMapping;

    [ObservableProperty]
    bool _launchOnStartup;

    [ObservableProperty]
    bool _showPeakVolumeBar;

    [ObservableProperty]
    bool _hotkeyCtrl;

    [ObservableProperty]
    bool _hotkeyAlt;

    [ObservableProperty]
    bool _hotkeyShift;

    [ObservableProperty]
    bool _hotkeyWin;

    public event Action<bool?> CloseRequested;

    public SettingsViewModel()
    {
        Mappings = new ObservableCollection<MappingEntry>();
        LoadSettings();
    }

    void LoadSettings()
    {
        LaunchOnStartup = Settings.Default.LaunchOnStartup;
        ShowPeakVolumeBar = Settings.Default.ShowPeakVolumeBar;
        LoadHotkeySettings();
        LoadMappings();
    }

    void LoadHotkeySettings()
    {
        HotkeyCtrl = Settings.Default.Hotkey_Ctrl;
        HotkeyAlt = Settings.Default.Hotkey_Alt;
        HotkeyShift = Settings.Default.Hotkey_Shift;
        HotkeyWin = Settings.Default.Hotkey_Win;
    }

    void LoadMappings()
    {
        Mappings.Clear();
        var loadedMappings = new Dictionary<string, MappingEntry>(StringComparer.OrdinalIgnoreCase);

        if(Settings.Default.ManualMappings == null) return;

        foreach(string mappingString in Settings.Default.ManualMappings)
        {
            if(string.IsNullOrWhiteSpace(mappingString)) continue;

            string[] parts = mappingString.Split(UIA_PROCESS_SEPARATOR);
            if(parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
                continue;

            string uiaName = parts[0].Trim();
            List<string> processNames = ParseProcessNames(parts[1]);

            if(processNames.Count == 0)
                continue;

            if(!loadedMappings.ContainsKey(uiaName))
            {
                var newEntry = new MappingEntry { UiaName = uiaName, ProcessNames = processNames };
                loadedMappings.Add(uiaName, newEntry);
                Mappings.Add(newEntry);
            }
            else
            {
                loadedMappings[uiaName].ProcessNames.AddRange(processNames);
                loadedMappings[uiaName].ProcessNames = loadedMappings[uiaName].ProcessNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }
        }
    }

    List<string> ParseProcessNames(string processNames) => processNames.Split(PROCESS_LIST_SEPARATOR, StringSplitOptions.RemoveEmptyEntries)
               .Select(p => p.Trim())
               .Where(p => !string.IsNullOrWhiteSpace(p))
               .Distinct(StringComparer.OrdinalIgnoreCase)
               .ToList();

    bool ValidateSettings()
    {
        if(!ValidateHotkeys()) return false;
        if(!ValidateMappingDuplicates()) return false;
        if(!ValidateEmptyMappings()) return false;
        return true;
    }

    bool ValidateHotkeys()
    {
        if(!HotkeyCtrl && !HotkeyAlt && !HotkeyShift && !HotkeyWin)
        {
            MessageBox.Show("Please select at least one modifier key for the activation hotkey.", "Invalid Hotkey", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        return true;
    }

    bool ValidateMappingDuplicates()
    {
        var duplicateUia = Mappings.GroupBy(m => m.UiaName, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).FirstOrDefault();
        if(duplicateUia != null)
        {
            MessageBox.Show($"The UIA Name '{duplicateUia}' is used more than once. Please remove duplicate entries.", "Duplicate Mapping Key", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        return true;
    }

    bool ValidateEmptyMappings()
    {
        var emptyMapping = Mappings.FirstOrDefault(m => string.IsNullOrWhiteSpace(m.UiaName) || m.ProcessNames == null || m.ProcessNames.Count == 0 || m.ProcessNames.All(string.IsNullOrWhiteSpace));
        if(emptyMapping != null)
        {
            MessageBox.Show($"Mapping for UIA Name '{emptyMapping?.UiaName ?? "<empty>"}' has no valid process names associated with it. Please edit or remove it.", "Invalid Mapping", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        return true;
    }

    void SaveSettings()
    {
        SaveStartupSetting();
        Settings.Default.ShowPeakVolumeBar = ShowPeakVolumeBar;
        SaveHotkeySettings();
        SaveMappings();
        TrySaveSettingsToStorage();
    }

    void SaveStartupSetting()
    {
        bool wantsStartup = LaunchOnStartup;
        if(Settings.Default.LaunchOnStartup == wantsStartup) return;

        Settings.Default.LaunchOnStartup = wantsStartup;
        try
        {
            if(wantsStartup) Native.OS_StartupManager.AddToStartup();
            else Native.OS_StartupManager.RemoveFromStartup();
        }
        catch(Exception ex)
        {
            MessageBox.Show($"Error updating startup setting: {ex.Message}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    void SaveHotkeySettings()
    {
        Settings.Default.Hotkey_Ctrl = HotkeyCtrl;
        Settings.Default.Hotkey_Alt = HotkeyAlt;
        Settings.Default.Hotkey_Shift = HotkeyShift;
        Settings.Default.Hotkey_Win = HotkeyWin;
    }

    void SaveMappings()
    {
        var settingsCollection = new StringCollection();
        foreach(MappingEntry entry in Mappings)
        {
            var validProcesses = entry.ProcessNames
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if(!string.IsNullOrWhiteSpace(entry.UiaName) && validProcesses.Count > 0)
                settingsCollection.Add($"{entry.UiaName.Trim()}{UIA_PROCESS_SEPARATOR}{string.Join(PROCESS_LIST_SEPARATOR.ToString(), validProcesses)}");
        }
        Settings.Default.ManualMappings = settingsCollection;
    }

    void TrySaveSettingsToStorage()
    {
        try { Settings.Default.Save(); }
        catch(Exception ex) { MessageBox.Show($"Error saving settings: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    [RelayCommand]
    void AddEdit()
    {
        var addDialog = new AddMappingWindow();
        if(addDialog.ShowDialog() != true) return;

        var mappingManager = new RightClickVolume.Managers.MappingManager(); // Fully qualify if namespace collision
        if(mappingManager.SaveOrUpdateManualMapping(addDialog.UiaName, addDialog.ProcessName))
        {
            LoadSettings(); // Reload all settings, including mappings
            MessageBox.Show($"Mapping for process '{addDialog.ProcessName}' added/updated under UIA Name '{addDialog.UiaName}'.", "Mapping Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    [RelayCommand]
    void Remove()
    {
        if(SelectedMapping == null)
        {
            MessageBox.Show("Please select a mapping entry to remove.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show($"Are you sure you want to remove the entire mapping for UIA Name '{SelectedMapping.UiaName}'?\n\nThis will remove the association for:\n{SelectedMapping.ProcessNameList}", "Confirm Removal", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if(result == MessageBoxResult.Yes)
        {
            Mappings.Remove(SelectedMapping);
            SelectedMapping = null;
        }
    }

    [RelayCommand]
    void Save()
    {
        if(ValidateSettings())
        {
            SaveSettings();
            CloseRequested?.Invoke(true);
        }
    }

    [RelayCommand]
    void Cancel() => CloseRequested?.Invoke(false);
}