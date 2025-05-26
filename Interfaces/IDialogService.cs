using System.Diagnostics;
using System.Windows;

namespace RightClickVolume.Interfaces;

public interface IDialogService
{
    bool? ShowSettingsWindow();
    (bool? DialogResult, string UiaName, string ProcessName) ShowAddMappingWindow(string uiaNameToMap = null);
    Process ShowProcessSelectorDialog();
    MessageBoxResult ShowMessageBox(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon);
}