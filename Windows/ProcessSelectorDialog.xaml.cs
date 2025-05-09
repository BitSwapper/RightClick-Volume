using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace RightClickVolume;

public partial class ProcessSelectorDialog : Window
{
    public Process SelectedProcess { get; private set; }

    public ProcessSelectorDialog()
    {
        InitializeComponent();
        LoadProcesses();
    }

    void LoadProcesses()
    {
        try
        {
            var processes = Process.GetProcesses().Where(p => p.Id != 0 && !string.IsNullOrEmpty(p.ProcessName) && p.MainWindowHandle != IntPtr.Zero).OrderBy(p => p.ProcessName).ToList();
            ProcessListView.ItemsSource = processes;
        }
        catch(Exception ex)
        {
            MessageBox.Show($"Error loading processes: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void SelectProcessAndClose()
    {
        var selectedItem = ProcessListView.SelectedItem as Process;
        if(selectedItem != null)
        {
            SelectedProcess = selectedItem;
            this.DialogResult = true;
        }
        else
            MessageBox.Show("Please select a process from the list.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    void SelectButton_Click(object sender, RoutedEventArgs e) => SelectProcessAndClose();

    void ProcessListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if(ProcessListView.SelectedItem != null)
            SelectProcessAndClose();
    }
}