using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Hardcodet.Wpf.TaskbarNotification;
using RightClickVolume.Managers;
using RightClickVolume.Properties;


namespace RightClickVolume;

public partial class App : Application
{
    TaskbarIcon notifyIcon;
    TaskbarMonitor taskbarMonitor;
    AudioManager audioManager;
    SettingsWindow settingsWindow = null;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Debug.AutoFlush = true;

        if(!InitializeAudioManager() || !InitializeTaskbarMonitor())
        {
            Shutdown();
            return;
        }

        CreateSystemTrayIcon();
    }

    bool InitializeAudioManager()
    {
        try
        {
            audioManager = new AudioManager();
            return true;
        }
        catch(Exception ex)
        {
            MessageBox.Show($"Fatal Error Initializing Audio Manager:\n{ex.Message}\n\nApplication will exit.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    bool InitializeTaskbarMonitor()
    {
        try
        {
            taskbarMonitor = new TaskbarMonitor(audioManager);
            taskbarMonitor.StartMonitoring();
            return true;
        }
        catch(Exception ex)
        {
            audioManager?.Dispose();
            MessageBox.Show($"Fatal Error Initializing Taskbar Monitor:\n{ex.Message}\n\nApplication will exit.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    void CreateSystemTrayIcon()
    {
        try
        {
            notifyIcon = new TaskbarIcon();
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
            notifyIcon.IconSource = new BitmapImage(new Uri("pack://application:,,,/Resources/AppIcon.ico"));
        }
        catch { }

        notifyIcon.ToolTipText = "RightClick Volume";
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

        notifyIcon.ContextMenu = contextMenu;
    }

    void ShowFirstRunNotification()
    {
        if(Settings.Default.IsFirstRunEver)
        {
            notifyIcon.ShowBalloonTip("RightClick Volume", "The application is now running in the background.", BalloonIcon.Info);
            Settings.Default.IsFirstRunEver = false;
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
        if(settingsWindow != null && settingsWindow.IsLoaded)
        {
            settingsWindow.Activate();
            return;
        }

        settingsWindow = new SettingsWindow();
        settingsWindow.ShowDialog();
        settingsWindow.Activate();
    }

    void ExitMenuItem_Click(object sender, RoutedEventArgs e) => Shutdown();

    protected override void OnExit(ExitEventArgs e)
    {
        CleanupResources();
        base.OnExit(e);
    }

    void CleanupResources()
    {
        try { settingsWindow?.Close(); } catch { }

        notifyIcon?.Dispose();

        taskbarMonitor?.StopMonitoring();
        (taskbarMonitor as IDisposable)?.Dispose();

        audioManager?.Dispose();
    }
}