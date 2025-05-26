using System;
using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RightClickVolume.Interfaces;

namespace RightClickVolume.ViewModels;

public partial class AddMappingViewModel : ObservableObject
{
    readonly IDialogService dialogService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OkCommand))]
    string uiaName;

    [ObservableProperty]
    Process selectedProcess;

    public string ProcessNameDisplay => SelectedProcess?.ProcessName;
    public Func<Process> ShowProcessSelectorDialogFunc { get; set; }
    public event Action<bool?> RequestCloseDialog;

    public AddMappingViewModel(string initialUiaName, IDialogService dialogService)
    {
        uiaName = initialUiaName;
        this.dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
    }

    partial void OnSelectedProcessChanged(Process value)
    {
        OnPropertyChanged(nameof(ProcessNameDisplay));
        OkCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    void BrowseProcess()
    {
        var process = ShowProcessSelectorDialogFunc?.Invoke();
        if(process != null)
        {
            SelectedProcess = process;
        }
    }

    bool CanOk() => !string.IsNullOrWhiteSpace(UiaName) && SelectedProcess != null;

    [RelayCommand(CanExecute = nameof(CanOk))]
    void Ok()
    {
        if(string.IsNullOrWhiteSpace(UiaName))
        {
            dialogService.ShowMessageBox("UIA Name cannot be empty.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if(SelectedProcess == null)
        {
            dialogService.ShowMessageBox("Please select a target process.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        RequestCloseDialog?.Invoke(true);
    }

    [RelayCommand]
    void Cancel() => RequestCloseDialog?.Invoke(false);
}