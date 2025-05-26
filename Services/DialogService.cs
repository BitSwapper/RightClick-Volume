using System.Diagnostics;
using System.Windows;
using RightClickVolume.Interfaces;

namespace RightClickVolume.Services;

public class DialogService : IDialogService
{
    readonly IViewModelFactory _viewModelFactory;

    public DialogService(IViewModelFactory viewModelFactory) => _viewModelFactory = viewModelFactory;

    public bool? ShowSettingsWindow()
    {
        var viewModel = _viewModelFactory.CreateSettingsViewModel();
        var window = new SettingsWindow
        {
            DataContext = viewModel
        };
        viewModel.CloseRequested += (result) =>
        {
            window.DialogResult = result;
            window.Close();
        };
        return window.ShowDialog();
    }

    public (bool? DialogResult, string UiaName, string ProcessName) ShowAddMappingWindow(string uiaNameToMap = null)
    {
        var viewModel = _viewModelFactory.CreateAddMappingViewModel(uiaNameToMap);
        var window = new AddMappingWindow(uiaNameToMap) // Pass uiaNameToMap if needed for direct use or keep ViewModel only
        {
            DataContext = viewModel
        };
        viewModel.ShowProcessSelectorDialogFunc = ShowProcessSelectorDialog; // Wire up the selector
        viewModel.RequestCloseDialog += (result) =>
        {
            window.DialogResult = result;
            // No explicit Close() here as DialogResult setter handles it for modal
        };

        bool? dialogResult = window.ShowDialog();
        if(dialogResult == true)
        {
            return (true, viewModel.UiaName, viewModel.SelectedProcess?.ProcessName);
        }
        return (dialogResult, null, null);
    }

    public Process ShowProcessSelectorDialog()
    {
        var viewModel = _viewModelFactory.CreateProcessSelectorViewModel();
        var dialog = new ProcessSelectorDialog()
        {
            DataContext = viewModel
        };
        viewModel.CloseRequested += (result) =>
        {
            dialog.DialogResult = result;
        };

        if(dialog.ShowDialog() == true)
        {
            return viewModel.SelectedProcess;
        }
        return null;
    }

    public MessageBoxResult ShowMessageBox(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon) => MessageBox.Show(messageBoxText, caption, button, icon);
}