using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Threading;
using RightClickVolume.Models;
using RightClickVolume.Native;
using RightClickVolume.Properties;

namespace RightClickVolume.Managers;

public class TaskbarMonitor : IDisposable
{
    const string ERROR_TITLE = "Error";
    const string AUDIO_SESSION_TITLE = "Audio Session Not Found";

    readonly uint currentProcessId;
    readonly AudioManager audioManager;
    readonly WindowsHooks windowsHooks;
    readonly UiaTaskbarScanner uiaScanner;
    readonly ProcessIdentifier processIdentifier;
    readonly MappingManager mappingManager;
    readonly VolumeKnobManager knobManager;

    CancellationTokenSource monitorCts;
    long isProcessingClick = 0;
    bool isDisposed = false;
    bool reqCtrl;
    bool reqAlt;
    bool reqShift;
    bool reqWin;

    public TaskbarMonitor(AudioManager audioManager)
    {
        try { currentProcessId = (uint)Process.GetCurrentProcess().Id; }
        catch { }

        LoadHotkeySettings();
        this.audioManager = audioManager ?? throw new ArgumentNullException(nameof(audioManager));
        windowsHooks = new WindowsHooks();
        uiaScanner = new UiaTaskbarScanner();
        mappingManager = new MappingManager();
        processIdentifier = new ProcessIdentifier(currentProcessId, mappingManager);
        knobManager = new VolumeKnobManager();
        windowsHooks.RightMouseClick += OnRightMouseClick;
    }

    public void StartMonitoring()
    {
        if(isDisposed) throw new ObjectDisposedException(nameof(TaskbarMonitor));
        if(!uiaScanner.IsInitialized) return;

        monitorCts = new CancellationTokenSource();
        windowsHooks.InstallMouseHook();
        knobManager.StartCleanupTask();
    }

    public void StopMonitoring()
    {
        if(isDisposed) return;
        windowsHooks.UninstallMouseHook();
        monitorCts?.Cancel();
        knobManager.StopCleanupTask();
        knobManager.HideAllKnobs();
    }

    void LoadHotkeySettings()
    {
        reqCtrl = Settings.Default.Hotkey_Ctrl;
        reqAlt = Settings.Default.Hotkey_Alt;
        reqShift = Settings.Default.Hotkey_Shift;
        reqWin = Settings.Default.Hotkey_Win;
    }

    void OnRightMouseClick(object sender, MouseHookEventArgs e)
    {
        if(isDisposed || !uiaScanner.IsInitialized) return;

        LoadHotkeySettings();

        if(!CheckHotkeyModifiers()) return;
        if(Interlocked.CompareExchange(ref isProcessingClick, 1, 0) != 0) return;

        int clickX = e.X;
        int clickY = e.Y;

        knobManager.HideAllKnobs();

        CancellationToken token = monitorCts?.Token ?? CancellationToken.None;
        Task.Run(async () => await ProcessRightClickAsync(clickX, clickY, token), token);
    }

    bool CheckHotkeyModifiers()
    {
        bool ctrlPressed = (Keyboard.GetKeyStates(Key.LeftCtrl) & KeyStates.Down) > 0 || (Keyboard.GetKeyStates(Key.RightCtrl) & KeyStates.Down) > 0;
        bool altPressed = (Keyboard.GetKeyStates(Key.LeftAlt) & KeyStates.Down) > 0 || (Keyboard.GetKeyStates(Key.RightAlt) & KeyStates.Down) > 0;
        bool shiftPressed = (Keyboard.GetKeyStates(Key.LeftShift) & KeyStates.Down) > 0 || (Keyboard.GetKeyStates(Key.RightShift) & KeyStates.Down) > 0;
        bool winPressed = (Keyboard.GetKeyStates(Key.LWin) & KeyStates.Down) > 0 || (Keyboard.GetKeyStates(Key.RWin) & KeyStates.Down) > 0;

        bool hotkeyMatch = (ctrlPressed == reqCtrl) && (altPressed == reqAlt) && (shiftPressed == reqShift) && (winPressed == reqWin);
        bool anyModifierRequired = reqCtrl || reqAlt || reqShift || reqWin;

        return hotkeyMatch && anyModifierRequired;
    }

    async Task ProcessRightClickAsync(int clickX, int clickY, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var clickPoint = new Point(clickX, clickY);
            AutomationElement clickedElement = uiaScanner.FindElementFromPoint(clickPoint, cancellationToken);
            if(clickedElement == null)
                return;

            AutomationElement taskbarElement = uiaScanner.FindTaskbarElement(clickedElement, cancellationToken);
            AutomationElement targetElement = taskbarElement ?? clickedElement;

            string uiaName = UiaHelper.GetElementNameSafe(targetElement);
            string extractedName = UiaHelper.ExtractAppNameFromTaskbarUiaName(uiaName);

            var identificationResult = processIdentifier.IdentifyProcess(targetElement, extractedName, cancellationToken);

            if(identificationResult.Success)
                HandleSuccessfulIdentification(clickX, clickY, identificationResult, cancellationToken);
            else
                await HandleFailedIdentification(uiaName, extractedName, cancellationToken);
        }
        catch(OperationCanceledException) { }
        catch(Exception ex)
        {
            await ShowMessageBoxAsync($"An unexpected error occurred: {ex.Message}", ERROR_TITLE, MessageBoxButton.OK, MessageBoxImage.Error, cancellationToken);
        }
        finally
        {
            Interlocked.Exchange(ref isProcessingClick, 0);
        }
    }

    async Task HandleSuccessfulIdentification(int clickX, int clickY, ProcessIdentifier.IdentificationResult identificationResult,
        CancellationToken cancellationToken)
    {
        AppAudioSession session = audioManager.GetAudioSessionForProcess(identificationResult.ProcessId);

        if(session != null)
            knobManager.ShowKnobForSession(clickX, clickY, session);
        else
        {
            string noSessionMessage = $"Found target process '{identificationResult.ApplicationName}' " + $"(PID {identificationResult.ProcessId}, Method: {identificationResult.Method}), " +  $"but it does not appear to have an active audio session.\n\nVolume knob cannot be shown.";
            await ShowMessageBoxAsync(noSessionMessage, AUDIO_SESSION_TITLE,
                MessageBoxButton.OK, MessageBoxImage.Information, cancellationToken);
        }
    }

    async Task HandleFailedIdentification(string uiaName, string extractedName, CancellationToken cancellationToken)
    {
        string nameToMap = (!string.IsNullOrWhiteSpace(extractedName) && extractedName != "[Error getting name]" && extractedName != "[Unknown]") ? extractedName : uiaName;
        nameToMap = nameToMap?.Trim();
        await mappingManager.PromptAndSaveMappingAsync(nameToMap, cancellationToken);
    }

    async Task ShowMessageBoxAsync(string message, string title, MessageBoxButton button,
        MessageBoxImage icon, CancellationToken token)
    {
        if(token.IsCancellationRequested) return;

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if(token.IsCancellationRequested || isDisposed ||
               Application.Current == null || Application.Current.Dispatcher.HasShutdownStarted) return;

            MessageBox.Show(message, title, button, icon);
        }, DispatcherPriority.Normal, token);
    }

    public void Dispose() => Dispose(true);

    protected virtual void Dispose(bool disposing)
    {
        if(!isDisposed)
        {
            if(disposing)
            {
                StopMonitoring();
                monitorCts?.Dispose();
                monitorCts = null;
                windowsHooks.RightMouseClick -= OnRightMouseClick;
                (windowsHooks as IDisposable)?.Dispose();
                knobManager?.Dispose();
            }
            isDisposed = true;
        }
    }
}