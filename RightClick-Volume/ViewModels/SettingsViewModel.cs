using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RightClickVolume.Interfaces;
using RightClickVolume.Models;


namespace RightClickVolume.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    const char UIA_PROCESS_SEPARATOR = '|';
    const char PROCESS_LIST_SEPARATOR = ';';

    readonly ISettingsService _settingsService;
    readonly IDialogService _dialogService;
    readonly IMappingManager _mappingManager;


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

    public SettingsViewModel(ISettingsService settingsService, IDialogService dialogService, IMappingManager mappingManager)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _mappingManager = mappingManager ?? throw new ArgumentNullException(nameof(mappingManager));

        Mappings = new ObservableCollection<MappingEntry>();
        LoadSettings();
    }

    void LoadSettings()
    {
        LaunchOnStartup = _settingsService.LaunchOnStartup;
        ShowPeakVolumeBar = _settingsService.ShowPeakVolumeBar;
        LoadHotkeySettings();
        LoadMappingsFromService();
    }

    void LoadHotkeySettings()
    {
        HotkeyCtrl = _settingsService.Hotkey_Ctrl;
        HotkeyAlt = _settingsService.Hotkey_Alt;
        HotkeyShift = _settingsService.Hotkey_Shift;
        HotkeyWin = _settingsService.Hotkey_Win;
    }

    void LoadMappingsFromService()
    {
        Mappings.Clear();
        var loadedMappingsData = _mappingManager.LoadManualMappings();

        var uniqueUiaNames = new Dictionary<string, MappingEntry>(StringComparer.OrdinalIgnoreCase);

        foreach(var kvp in loadedMappingsData)
        {
            string uiaName = kvp.Key;
            List<string> processNames = kvp.Value;

            if(processNames.Count == 0) continue;

            if(!uniqueUiaNames.ContainsKey(uiaName))
            {
                var newEntry = new MappingEntry { UiaName = uiaName, ProcessNames = new List<string>(processNames) };
                uniqueUiaNames.Add(uiaName, newEntry);
                Mappings.Add(newEntry);
            }
            else
            {
                uniqueUiaNames[uiaName].ProcessNames.AddRange(processNames);
                uniqueUiaNames[uiaName].ProcessNames = uniqueUiaNames[uiaName].ProcessNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                var existingEntryInMappings = Mappings.FirstOrDefault(m => m.UiaName.Equals(uiaName, StringComparison.OrdinalIgnoreCase));
                if(existingEntryInMappings != null)
                {
                    existingEntryInMappings.ProcessNames = new List<string>(uniqueUiaNames[uiaName].ProcessNames);

                }
            }
        }
    }


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
            _dialogService.ShowMessageBox("Please select at least one modifier key for the activation hotkey.", "Invalid Hotkey", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        return true;
    }

    bool ValidateMappingDuplicates()
    {
        var duplicateUia = Mappings.GroupBy(m => m.UiaName, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).FirstOrDefault();
        if(duplicateUia != null)
        {
            _dialogService.ShowMessageBox($"The UIA Name '{duplicateUia}' is used more than once. Please remove duplicate entries.", "Duplicate Mapping Key", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        return true;
    }

    bool ValidateEmptyMappings()
    {
        var emptyMapping = Mappings.FirstOrDefault(m => string.IsNullOrWhiteSpace(m.UiaName) || m.ProcessNames == null || m.ProcessNames.Count == 0 || m.ProcessNames.All(string.IsNullOrWhiteSpace));
        if(emptyMapping != null)
        {
            _dialogService.ShowMessageBox($"Mapping for UIA Name '{emptyMapping?.UiaName ?? "<empty>"}' has no valid process names associated with it. Please edit or remove it.", "Invalid Mapping", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        return true;
    }

    void SaveSettingsToService()
    {
        SaveStartupSetting();
        _settingsService.ShowPeakVolumeBar = ShowPeakVolumeBar;
        SaveHotkeySettingsToService();
        SaveMappingsToService();
        TrySaveSettingsToStorage();
    }

    void SaveStartupSetting()
    {
        bool wantsStartup = LaunchOnStartup;
        if(_settingsService.LaunchOnStartup == wantsStartup) return;

        _settingsService.LaunchOnStartup = wantsStartup;
        try
        {
            if(wantsStartup) Native.OS_StartupManager.AddToStartup();
            else Native.OS_StartupManager.RemoveFromStartup();
        }
        catch(Exception ex)
        {
            _dialogService.ShowMessageBox($"Error updating startup setting: {ex.Message}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    void SaveHotkeySettingsToService()
    {
        _settingsService.Hotkey_Ctrl = HotkeyCtrl;
        _settingsService.Hotkey_Alt = HotkeyAlt;
        _settingsService.Hotkey_Shift = HotkeyShift;
        _settingsService.Hotkey_Win = HotkeyWin;
    }

    void SaveMappingsToService()
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
        _settingsService.ManualMappings = settingsCollection;
    }

    void TrySaveSettingsToStorage()
    {
        try { _settingsService.Save(); }
        catch(Exception ex) { _dialogService.ShowMessageBox($"Error saving settings: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    [RelayCommand]
    void AddEdit()
    {
        var (dialogResult, uiaName, processName) = _dialogService.ShowAddMappingWindow();
        if(dialogResult != true) return;

        if(_mappingManager.SaveOrUpdateManualMapping(uiaName, processName))
        {
            LoadSettings();
            _dialogService.ShowMessageBox($"Mapping for process '{processName}' added/updated under UIA Name '{uiaName}'.", "Mapping Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    [RelayCommand]
    void Remove()
    {
        if(SelectedMapping == null)
        {
            _dialogService.ShowMessageBox("Please select a mapping entry to remove.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = _dialogService.ShowMessageBox($"Are you sure you want to remove the entire mapping for UIA Name '{SelectedMapping.UiaName}'?\n\nThis will remove the association for:\n{SelectedMapping.ProcessNameList}", "Confirm Removal", MessageBoxButton.YesNo, MessageBoxImage.Warning);

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
            SaveSettingsToService();
            CloseRequested?.Invoke(true);
        }
    }

    [RelayCommand]
    void Cancel() => CloseRequested?.Invoke(false);
}