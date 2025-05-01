using System;
using System.Windows;
namespace RightClickVolume;

public partial class AddMappingWindow : Window
{
    public string UiaName { get; set; }
    public string ProcessName { get; set; }
    public AddMappingWindow(string initialUiaName = null)
    {
        InitializeComponent();
        if(!string.IsNullOrWhiteSpace(initialUiaName))
            UiaNameTextBox.Text = initialUiaName;

        ProcessNameTextBox.Focus();
    }

    void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var processDialog = new ProcessSelectorDialog();
        processDialog.Owner = this;
        if(processDialog.ShowDialog() == true)
            if(processDialog.SelectedProcess != null)
                ProcessNameTextBox.Text = processDialog.SelectedProcess.ProcessName;
    }

    void OkButton_Click(object sender, RoutedEventArgs e)
    {
        string uiaName = UiaNameTextBox.Text.Trim();
        string processName = ProcessNameTextBox.Text.Trim();
        if(string.IsNullOrWhiteSpace(uiaName))
        {
            MessageBox.Show("Please enter the UIA Name (Key).", "Input Missing", MessageBoxButton.OK, MessageBoxImage.Warning);
            UiaNameTextBox.Focus();
            return;
        }
        if(string.IsNullOrWhiteSpace(processName))
        {
            MessageBox.Show("Please enter or select the Target Process Name.", "Input Missing", MessageBoxButton.OK, MessageBoxImage.Warning);
            ProcessNameTextBox.Focus();
            return;
        }
        if(processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("Please enter the Target Process Name *without* the '.exe' extension.", "Invalid Format", MessageBoxButton.OK, MessageBoxImage.Warning);
            ProcessNameTextBox.Focus();
            return;
        }
        this.UiaName = uiaName;
        this.ProcessName = processName;
        this.DialogResult = true;
    }
}