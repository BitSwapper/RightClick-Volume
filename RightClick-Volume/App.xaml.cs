using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Hardcodet.Wpf.TaskbarNotification;
using RightClickVolume.Interfaces;
using RightClickVolume.Managers;
using RightClickVolume.Native;
using RightClickVolume.Properties;
using RightClickVolume.Services;
using RightClickVolume.ViewModels;
using Microsoft.Extensions.DependencyInjection;


namespace RightClickVolume;

public partial class App : Application
{
    public static IServiceProvider ServiceProvider { get; private set; }

    private TaskbarIcon _notifyIcon;
    private ITaskbarMonitor _taskbarMonitor;
    private IAudioManager _audioManager;
    private ISettingsService _settingsService;
    private IDialogService _dialogService;


    public App()
    {
        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        ServiceProvider = serviceCollection.BuildServiceProvider();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IAudioManager, AudioManager>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IMappingManager, MappingManager>();

        services.AddSingleton<IWindowsHookService, WindowsHooks>();
        services.AddSingleton<IUiaScannerService, UiaTaskbarScanner>();
        services.AddSingleton<IVolumeKnobManager, VolumeKnobManager>();

        services.AddSingleton<ITaskbarMonitor, TaskbarMonitor>();
        services.AddSingleton<IViewModelFactory, ViewModelFactory>();

        services.AddTransient<SettingsViewModel>();
        services.AddTransient<AddMappingViewModel>();
        services.AddTransient<ProcessSelectorViewModel>();
        services.AddTransient<VolumeKnobViewModel>();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Debug.AutoFlush = true;

        _settingsService = ServiceProvider.GetRequiredService<ISettingsService>();
        _dialogService = ServiceProvider.GetRequiredService<IDialogService>();
        _audioManager = ServiceProvider.GetRequiredService<IAudioManager>();

        if(!InitializeTaskbarMonitorViaDI())
        {
            Shutdown();
            return;
        }

        CreateSystemTrayIcon();
    }

    bool InitializeTaskbarMonitorViaDI()
    {
        try
        {
            _taskbarMonitor = ServiceProvider.GetRequiredService<ITaskbarMonitor>();
            _taskbarMonitor.StartMonitoring();
            return true;
        }
        catch(Exception ex)
        {
            _audioManager?.Dispose();
            MessageBox.Show($"Fatal Error Initializing Taskbar Monitor:\n{ex.Message}\n\nApplication will exit.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    void CreateSystemTrayIcon()
    {
        try
        {
            _notifyIcon = new TaskbarIcon();
            SetTaskbarIconImage();
            ConfigureContextMenu();
            ShowFirstRunNotification();
        }
        catch(Exception ex)
        {
            MessageBox.Show($"Error creating system tray icon: {ex.Message}\n\nFunctionality may be limited.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    void SetTaskbarIconImage()
    {
        try
        {
            _notifyIcon.IconSource = new BitmapImage(new Uri("pack://application:,,,/Resources/AppIcon.ico"));
        }
        catch(Exception ex)
        {
            Debug.WriteLine($"Error setting taskbar icon image: {ex.Message}");
        }
        _notifyIcon.ToolTipText = StaticVals.AppName;
    }

    void ConfigureContextMenu()
    {
        var link = new MenuItem { Header = "Made by Github.com/BitSwapper..." };
        link.Click += OpenLink;

        var settingsMenuItem = new MenuItem { Header = "Settings..." };
        settingsMenuItem.Click += SettingsMenuItem_Click;

        var exitMenuItem = new MenuItem { Header = "Exit" };
        exitMenuItem.Click += ExitMenuItem_Click;

        var contextMenu = new ContextMenu();
        contextMenu.Items.Add(link);
        contextMenu.Items.Add(settingsMenuItem);
        contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(exitMenuItem);

        _notifyIcon.ContextMenu = contextMenu;
    }

    void ShowFirstRunNotification()
    {
        if(Settings.Default.IsFirstRunEver)
        {
            Settings.Default.IsFirstRunEver = false;
            Settings.Default.Save();
        }
    }

    void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        OpenSettingsWindow();
    }

    void OpenLink(object sender, RoutedEventArgs e)
    {
        string url = "https://github.com/BitSwapper";
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch(Exception ex)
        {
            MessageBox.Show($"Could not open the link: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void OpenSettingsWindow()
    {
        if(_dialogService != null)
        {
            _dialogService.ShowSettingsWindow();
        }
        else
        {
            MessageBox.Show("Dialog service not available.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void ExitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ShutdownApplication();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        CleanupResources();
        base.OnExit(e);
    }

    private void ShutdownApplication()
    {
        CleanupResources();
        Shutdown();
    }

    void CleanupResources()
    {
        _notifyIcon?.Dispose();
        _notifyIcon = null;

        _taskbarMonitor?.StopMonitoring();
        _taskbarMonitor?.Dispose();
        _taskbarMonitor = null;

        _audioManager?.Dispose();
        _audioManager = null;
    }
}