// ProcessSelectorDialog.xaml.cs
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using RightClickVolume.ViewModels;

namespace RightClickVolume;

public partial class ProcessSelectorDialog : Window
{
    ProcessSelectorViewModel _viewModel;
    public Process SelectedProcess => _viewModel?.SelectedProcess;

    public ProcessSelectorDialog()
    {
        InitializeComponent();
        _viewModel = new ProcessSelectorViewModel();
        DataContext = _viewModel;
        _viewModel.CloseRequested += (dialogResult) =>
        {
            this.DialogResult = dialogResult;
            // No explicit Close() here, DialogResult setter handles it for modal dialogs
        };
    }

    // SelectButton_Click is handled by Command
    // CancelButton_Click is handled by IsCancel=True or Command

    void ProcessListView_MouseDoubleClick(object sender, MouseButtonEventArgs e) => _viewModel.HandleDoubleClick();
}