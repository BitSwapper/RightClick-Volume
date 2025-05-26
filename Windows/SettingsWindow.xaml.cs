// SettingsWindow.xaml.cs
using System.Collections.Generic;
using System.Windows;
using RightClickVolume.ViewModels;

namespace RightClickVolume;

public partial class SettingsWindow : Window
{
    SettingsViewModel _viewModel;

    public SettingsWindow()
    {
        InitializeComponent();
        _viewModel = new SettingsViewModel();
        DataContext = _viewModel;
        _viewModel.CloseRequested += (dialogResult) =>
        {
            this.DialogResult = dialogResult;
            this.Close();
        };
    }
}

public class MappingEntry // This class can stay as is, or implement ObservableObject if its properties need to be editable directly in the ListView and reflect changes. For now, it's mostly read-only display.
{
    public string UiaName { get; set; }
    public List<string> ProcessNames { get; set; } = new List<string>();
    public string ProcessNameList => string.Join("; ", ProcessNames);
}