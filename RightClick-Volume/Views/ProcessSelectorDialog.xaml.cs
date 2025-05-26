using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using RightClickVolume.ViewModels;

namespace RightClickVolume;

public partial class ProcessSelectorDialog : Window
{
    public Process SelectedProcess => (DataContext as ProcessSelectorViewModel)?.SelectedProcess;

    public ProcessSelectorDialog() => InitializeComponent();

    void ProcessListView_MouseDoubleClick(object sender, MouseButtonEventArgs e) => (DataContext as ProcessSelectorViewModel)?.HandleDoubleClick();
}