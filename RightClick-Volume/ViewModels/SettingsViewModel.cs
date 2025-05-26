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

    private readonly ISettingsService settingsService;
    private readonly IDialogService dialogService;
    private readonly IMappingManager mappingManager;


    [ObservableProperty]
    private ObservableCollection<MappingEntry> mappings;

    [ObservableProperty]
    private MappingEntry selectedMapping;

    [ObservableProperty]
    private bool launchOnStartup;

    [ObservableProperty]
    private bool showPeakVolumeBar;

    [ObservableProperty]
    private bool hotkeyCtrl;

    [ObservableProperty]
    private bool hotkeyAlt;

    [ObservableProperty]
    private bool hotkeyShift;

    [ObservableProperty]
    private bool hotkeyWin;

    public event Action<bool?> CloseRequested;

    public SettingsViewModel(ISettingsService settingsService, IDialogService dialogService, IMappingManager mappingManager)
    {
        this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        this.dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        this.mappingManager = mappingManager ?? throw new ArgumentNullException(nameof(mappingManager));

        Mappings = new ObservableCollection<MappingEntry>();
        LoadSettings();
    }

    private void LoadSettings()
    {
        LaunchOnStartup = settingsService.LaunchOnStartup;
        ShowPeakVolumeBar = settingsService.ShowPeakVolumeBar;
        LoadHotkeySettings();
        LoadMappingsFromService();
    }

    private void LoadHotkeySettings()
    {
        HotkeyCtrl = settingsService.Hotkey_Ctrl;
        HotkeyAlt = settingsService.Hotkey_Alt;
        HotkeyShift = settingsService.Hotkey_Shift;
        HotkeyWin = settingsService.Hotkey_Win;
    }

    private void LoadMappingsFromService()
    {
        Mappings.Clear();
        var loadedMappingsData = mappingManager.LoadManualMappings();

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

    private bool ValidateSettings()
    {
        if(!ValidateHotkeys()) return false;
        if(!ValidateMappingDuplicates()) return false;
        if(!ValidateEmptyMappings()) return false;
        return true;
    }

    private bool ValidateHotkeys()
    {
        if(!HotkeyCtrl && !HotkeyAlt && !HotkeyShift && !HotkeyWin)
        {
            dialogService.ShowMessageBox("Please select at least one modifier key for the activation hotkey.", "Invalid Hotkey", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        return true;
    }

    private bool ValidateMappingDuplicates()
    {
        var duplicateUia = Mappings.GroupBy(m => m.UiaName, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).FirstOrDefault();
        if(duplicateUia != null)
        {
            dialogService.ShowMessageBox($"The UIA Name '{duplicateUia}' is used more than once. Please remove duplicate entries.", "Duplicate Mapping Key", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        return true;
    }

    private bool ValidateEmptyMappings()
    {
        var emptyMapping = Mappings.FirstOrDefault(m => string.IsNullOrWhiteSpace(m.UiaName) || m.ProcessNames == null || m.ProcessNames.Count == 0 || m.ProcessNames.All(string.IsNullOrWhiteSpace));
        if(emptyMapping != null)
        {
            dialogService.ShowMessageBox($"Mapping for UIA Name '{emptyMapping?.UiaName ?? "<empty>"}' has no valid process names associated with it. Please edit or remove it.", "Invalid Mapping", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        return true;
    }

    private void SaveSettingsToService()
    {
        SaveStartupSetting();
        settingsService.ShowPeakVolumeBar = ShowPeakVolumeBar;
        SaveHotkeySettingsToService();
        SaveMappingsToService();
        TrySaveSettingsToStorage();
    }

    private void SaveStartupSetting()
    {
        bool wantsStartup = this.LaunchOnStartup;

        if(settingsService.LaunchOnStartup == wantsStartup) return;

        settingsService.LaunchOnStartup = wantsStartup;
        try
        {
            if(wantsStartup) Native.OS_StartupManager.AddToStartup();
            else Native.OS_StartupManager.RemoveFromStartup();
        }
        catch(Exception ex)
        {
            dialogService.ShowMessageBox($"Error updating startup setting: {ex.Message}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SaveHotkeySettingsToService()
    {
        settingsService.Hotkey_Ctrl = HotkeyCtrl;
        settingsService.Hotkey_Alt = HotkeyAlt;
        settingsService.Hotkey_Shift = HotkeyShift;
        settingsService.Hotkey_Win = HotkeyWin;
    }

    private void SaveMappingsToService()
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
        settingsService.ManualMappings = settingsCollection;
    }

    private void TrySaveSettingsToStorage()
    {
        try { settingsService.Save(); }
        catch(Exception ex) { dialogService.ShowMessageBox($"Error saving settings: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    [RelayCommand]
    private void AddEdit()
    {
        var (dialogResult, uiaName, processName) = dialogService.ShowAddMappingWindow();
        if(dialogResult != true) return;

        if(mappingManager.SaveOrUpdateManualMapping(uiaName, processName))
        {
            LoadSettings();
            dialogService.ShowMessageBox($"Mapping for process '{processName}' added/updated under UIA Name '{uiaName}'.", "Mapping Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    [RelayCommand]
    private void Remove()
    {
        if(SelectedMapping == null)
        {
            dialogService.ShowMessageBox("Please select a mapping entry to remove.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = dialogService.ShowMessageBox($"Are you sure you want to remove the entire mapping for UIA Name '{SelectedMapping.UiaName}'?\n\nThis will remove the association for:\n{SelectedMapping.ProcessNameList}", "Confirm Removal", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if(result == MessageBoxResult.Yes)
        {
            Mappings.Remove(SelectedMapping);
            SelectedMapping = null;
        }
    }

    [RelayCommand]
    private void Save()
    {
        if(ValidateSettings())
        {
            SaveSettingsToService();
            CloseRequested?.Invoke(true);
        }
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(false);
}