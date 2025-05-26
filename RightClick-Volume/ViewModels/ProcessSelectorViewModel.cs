using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RightClickVolume.Interfaces;

namespace RightClickVolume.ViewModels;

public partial class ProcessSelectorViewModel : ObservableObject
{
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private ObservableCollection<Process> _processes;

    [ObservableProperty]
    private Process _selectedProcess;

    public event Action<bool?> CloseRequested;

    public ProcessSelectorViewModel(IDialogService dialogService)
    {
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        Processes = new ObservableCollection<Process>();
        LoadProcesses();
    }

    private void LoadProcesses()
    {
        try
        {
            Processes.Clear();
            var processList = System.Diagnostics.Process.GetProcesses()
                .Where(p => p.Id != 0 && !string.IsNullOrEmpty(p.ProcessName) && p.MainWindowHandle != IntPtr.Zero)
                .OrderBy(p => p.ProcessName)
                .ToList();
            foreach(var p in processList)
            {
                Processes.Add(p);
            }
        }
        catch(Exception ex)
        {
            _dialogService.ShowMessageBox($"Error loading processes: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void Select()
    {
        if(SelectedProcess != null)
        {
            CloseRequested?.Invoke(true);
        }
        else
        {
            _dialogService.ShowMessageBox("Please select a process from the list.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(false);
    }

    public void HandleDoubleClick()
    {
        if(SelectedProcess != null)
        {
            SelectCommand.Execute(null);
        }
    }
}