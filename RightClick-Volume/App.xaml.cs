using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Hardcodet.Wpf.TaskbarNotification; // Assuming this is your TaskbarIcon library
using Microsoft.Extensions.DependencyInjection; // For DI
using RightClickVolume.Interfaces;
using RightClickVolume.Managers;
using RightClickVolume.Properties;
using RightClickVolume.Services; // For ViewModelFactory and DialogService
using RightClickVolume.ViewModels; // For ViewModels if needed directly (less common here)


namespace RightClickVolume;

public partial class App : Application
{
    public static IServiceProvider ServiceProvider { get; private set; }

    TaskbarIcon _notifyIcon; // Using Hardcodet.Wpf.TaskbarNotification
    ITaskbarMonitor _taskbarMonitor;
    IAudioManager _audioManager;
    ISettingsService _settingsService;
    IDialogService _dialogService;

    SettingsWindow _currentSettingsWindow = null; // To keep track of the settings window instance

    public App()
    {
        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        ServiceProvider = serviceCollection.BuildServiceProvider();
    }

    void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IAudioManager, AudioManager>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IMappingManager, MappingManager>();
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


        if(!InitializeAudioManagerViaDI() || !InitializeTaskbarMonitorViaDI())
        {
            Shutdown();
            return;
        }

        CreateSystemTrayIcon();
    }

    bool InitializeAudioManagerViaDI()
    {
        try
        {
            _audioManager = ServiceProvider.GetRequiredService<IAudioManager>();
            return _audioManager != null;
        }
        catch(Exception ex)
        {
            MessageBox.Show($"Fatal Error Initializing Audio Manager:\n{ex.Message}\n\nApplication will exit.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
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
        if(_settingsService.LaunchOnStartup && Settings.Default.IsFirstRunEver) // Assuming IsFirstRunEver is still in Settings.Default or move to ISettingsService
        {
            _notifyIcon.ShowBalloonTip(StaticVals.AppName, "The application is now running in the background.", BalloonIcon.Info);
            Settings.Default.IsFirstRunEver = false; // Keep this if it's a one-time flag not part of ISettingsService general props
            Settings.Default.Save();
        }
    }

    void SettingsMenuItem_Click(object sender, RoutedEventArgs e) => OpenSettingsWindow();

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
        if(_currentSettingsWindow != null && _currentSettingsWindow.IsLoaded)
        {
            _currentSettingsWindow.Activate();
            return;
        }

        if(_dialogService != null)
        {
            _dialogService.ShowSettingsWindow();
        }
        else
        {
            MessageBox.Show("Dialog service not available.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }


    void ExitMenuItem_Click(object sender, RoutedEventArgs e) => ShutdownApplication();

    protected override void OnExit(ExitEventArgs e)
    {
        CleanupResources();
        base.OnExit(e);
    }

    void ShutdownApplication()
    {
        CleanupResources();
        Shutdown();
    }


    void CleanupResources()
    {
        try { _currentSettingsWindow?.Close(); } catch { }
        _currentSettingsWindow = null;

        _notifyIcon?.Dispose();
        _notifyIcon = null;

        _taskbarMonitor?.StopMonitoring();
        _taskbarMonitor?.Dispose(); // ITaskbarMonitor interface inherits IDisposable
        _taskbarMonitor = null;

        _audioManager?.Dispose(); // IAudioManager interface inherits IDisposable
        _audioManager = null;
    }
}