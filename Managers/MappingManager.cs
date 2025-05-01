using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using RightClickVolume.Properties;

namespace RightClickVolume.Managers;

internal class MappingManager
{
    const char UIA_PROCESS_SEPARATOR = '|';
    const char PROCESS_LIST_SEPARATOR = ';';

    public Dictionary<string, List<string>> LoadManualMappings()
    {
        var mappings = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if(Settings.Default.ManualMappings == null) return mappings;

            foreach(string mappingString in Settings.Default.ManualMappings)
                TryParseAndAddMapping(mappingString, mappings);
        }
        catch { }

        return mappings;
    }

    void TryParseAndAddMapping(string mappingString, Dictionary<string, List<string>> mappings)
    {
        if(string.IsNullOrWhiteSpace(mappingString)) return;

        string[] parts = mappingString.Split(UIA_PROCESS_SEPARATOR);
        if(parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
            return;

        string uiaName = parts[0].Trim();
        List<string> processNames = ParseProcessNames(parts[1]);

        if(processNames.Count > 0)
            mappings[uiaName] = processNames;
    }

    List<string> ParseProcessNames(string processNamesString) => processNamesString
            .Split(PROCESS_LIST_SEPARATOR, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public bool SaveOrUpdateManualMapping(string uiaName, string processNameToAdd)
    {
        if(string.IsNullOrWhiteSpace(uiaName) || string.IsNullOrWhiteSpace(processNameToAdd))
            return false;

        uiaName = uiaName.Trim();
        processNameToAdd = processNameToAdd.Trim();

        try
        {
            var currentMappings = LoadManualMappings();
            bool changed = AddOrUpdateMapping(currentMappings, uiaName, processNameToAdd);

            if(changed)
                SaveMappingsToSettings(currentMappings);

            return true;
        }
        catch(Exception ex)
        {
            MessageBox.Show($"Failed to save the mapping: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    bool AddOrUpdateMapping(Dictionary<string, List<string>> mappings, string uiaName, string processName)
    {
        if(mappings.TryGetValue(uiaName, out List<string> existingProcesses))
        {
            if(!existingProcesses.Contains(processName, StringComparer.OrdinalIgnoreCase))
            {
                existingProcesses.Add(processName);
                return true;
            }
            return false;
        }

        mappings[uiaName] = new List<string> { processName };
        return true;
    }

    void SaveMappingsToSettings(Dictionary<string, List<string>> mappings)
    {
        var settingsCollection = new StringCollection();

        foreach(var kvp in mappings)
            if(kvp.Value?.Count > 0)
                settingsCollection.Add($"{kvp.Key}{UIA_PROCESS_SEPARATOR}{string.Join(PROCESS_LIST_SEPARATOR.ToString(), kvp.Value)}");

        Settings.Default.ManualMappings = settingsCollection;
        Settings.Default.Save();
    }

    public async Task PromptAndSaveMappingAsync(string uiaNameToMap, CancellationToken cancellationToken)
    {
        string threadId = $"[BG Thread {Thread.CurrentThread.ManagedThreadId}]";
        await Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            if(ShouldAbortPrompt(cancellationToken))
                return;

            if(!PromptUserForMapping(uiaNameToMap, threadId))
                return;

            ShowAddMappingDialog(uiaNameToMap, threadId);
        }, DispatcherPriority.Normal, cancellationToken);
    }

    bool ShouldAbortPrompt(CancellationToken cancellationToken) => Application.Current == null || Application.Current.Dispatcher.HasShutdownStarted || cancellationToken.IsCancellationRequested;

    bool PromptUserForMapping(string uiaNameToMap, string threadId)
    {
        string promptMessage = $"Could not automatically find an audio process for the clicked item:\n\nUIA Name: '{uiaNameToMap}'\n\nDo you want to manually map this name to a specific running process?";
        MessageBoxResult result = MessageBox.Show(promptMessage, "Manual Mapping Needed", MessageBoxButton.YesNo, MessageBoxImage.Question);

        return result == MessageBoxResult.Yes;
    }

    void ShowAddMappingDialog(string uiaNameToMap, string threadId)
    {
        try
        {
            var addDialog = new AddMappingWindow(uiaNameToMap);
            HandleMappingDialogResult(addDialog, threadId);
        }
        catch(Exception ex)
        {
            MessageBox.Show($"Failed to open the mapping window: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void HandleMappingDialogResult(AddMappingWindow dialog, string threadId)
    {
        if(dialog.ShowDialog() != true)
            return;

        if(SaveOrUpdateManualMapping(dialog.UiaName, dialog.ProcessName))
            MessageBox.Show($"Mapping for process '{dialog.ProcessName}' saved/updated under UIA Name:\n'{dialog.UiaName}'\n\nPlease try Ctrl+Right-clicking the item again.", "Mapping Saved/Updated", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}