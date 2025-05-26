using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Threading;
using RightClickVolume.Interfaces;
using RightClickVolume.Models;
using RightClickVolume.Native;


namespace RightClickVolume.Managers;

public class TaskbarMonitor : ITaskbarMonitor
{
    const string ERROR_TITLE = "Error";
    const string AUDIO_SESSION_TITLE = "Audio Session Not Found";

    readonly uint currentProcessId;
    readonly IAudioManager _audioManager;
    readonly IWindowsHookService _windowsHookService;
    readonly IUiaScannerService _uiaScannerService;
    readonly ProcessIdentifier _processIdentifier;
    readonly IMappingManager _mappingManager;
    readonly IVolumeKnobManager _knobManager;
    private readonly IDialogService _dialogService;
    private readonly ISettingsService _settingsService;

    CancellationTokenSource monitorCts;
    long isProcessingClick = 0;
    bool isDisposed = false;
    bool reqCtrl;
    bool reqAlt;
    bool reqShift;
    bool reqWin;

    public TaskbarMonitor(
        IAudioManager audioManager,
        IMappingManager mappingManager,
        IDialogService dialogService,
        ISettingsService settingsService,
        IWindowsHookService windowsHookService,
        IUiaScannerService uiaScannerService,
        IVolumeKnobManager volumeKnobManager)
    {
        try { currentProcessId = (uint)Process.GetCurrentProcess().Id; }
        catch { }

        _audioManager = audioManager ?? throw new ArgumentNullException(nameof(audioManager));
        _mappingManager = mappingManager ?? throw new ArgumentNullException(nameof(mappingManager));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _windowsHookService = windowsHookService ?? throw new ArgumentNullException(nameof(windowsHookService));
        _uiaScannerService = uiaScannerService ?? throw new ArgumentNullException(nameof(uiaScannerService));
        _knobManager = volumeKnobManager ?? throw new ArgumentNullException(nameof(volumeKnobManager));

        _processIdentifier = new ProcessIdentifier(currentProcessId, _mappingManager);

        _windowsHookService.RightMouseClick += OnRightMouseClick;

        LoadHotkeySettings();
        _settingsService.PropertyChanged += (s, e) => {
            if(e.PropertyName == nameof(ISettingsService.Hotkey_Alt) ||
                e.PropertyName == nameof(ISettingsService.Hotkey_Ctrl) ||
                e.PropertyName == nameof(ISettingsService.Hotkey_Shift) ||
                e.PropertyName == nameof(ISettingsService.Hotkey_Win))
            {
                LoadHotkeySettings();
            }
        };
    }

    public void StartMonitoring()
    {
        if(isDisposed) throw new ObjectDisposedException(nameof(TaskbarMonitor));
        if(!_uiaScannerService.IsInitialized) return;

        monitorCts = new CancellationTokenSource();
        _windowsHookService.InstallMouseHook();
        _knobManager.StartCleanupTask();
    }

    public void StopMonitoring()
    {
        if(isDisposed) return;
        _windowsHookService.UninstallMouseHook();
        monitorCts?.Cancel();
        _knobManager.StopCleanupTask();
        _knobManager.HideAllKnobs();
    }

    void LoadHotkeySettings()
    {
        reqCtrl = _settingsService.Hotkey_Ctrl;
        reqAlt = _settingsService.Hotkey_Alt;
        reqShift = _settingsService.Hotkey_Shift;
        reqWin = _settingsService.Hotkey_Win;
    }

    void OnRightMouseClick(object sender, MouseHookEventArgs e)
    {
        if(isDisposed || !_uiaScannerService.IsInitialized) return;

        if(!CheckHotkeyModifiers()) return;
        if(Interlocked.CompareExchange(ref isProcessingClick, 1, 0) != 0) return;

        int clickX = e.X;
        int clickY = e.Y;

        _knobManager.HideAllKnobs();

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
            AutomationElement clickedElement = _uiaScannerService.FindElementFromPoint(clickPoint, cancellationToken);
            if(clickedElement == null)
                return;

            AutomationElement taskbarElement = _uiaScannerService.FindTaskbarElement(clickedElement, cancellationToken);
            AutomationElement targetElement = taskbarElement ?? clickedElement;

            string uiaName = UiaHelper.GetElementNameSafe(targetElement);
            string extractedName = UiaHelper.ExtractAppNameFromTaskbarUiaName(uiaName);

            var identificationResult = _processIdentifier.IdentifyProcess(targetElement, extractedName, cancellationToken);

            if(identificationResult.Success)
                await HandleSuccessfulIdentification(clickX, clickY, identificationResult, cancellationToken);
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
        AppAudioSession session = _audioManager.GetAudioSessionForProcess(identificationResult.ProcessId);

        if(session != null)
            _knobManager.ShowKnobForSession(clickX, clickY, session);
        else
        {
            string noSessionMessage = $"Found target process '{identificationResult.ApplicationName}' " + $"(PID {identificationResult.ProcessId}, Method: {identificationResult.Method}), " + $"but it does not appear to have an active audio session.\n\nVolume knob cannot be shown.";
            await ShowMessageBoxAsync(noSessionMessage, AUDIO_SESSION_TITLE,
                MessageBoxButton.OK, MessageBoxImage.Information, cancellationToken);
        }
    }

    async Task HandleFailedIdentification(string uiaName, string extractedName, CancellationToken cancellationToken)
    {
        string nameToMap = (!string.IsNullOrWhiteSpace(extractedName) && extractedName != "[Error getting name]" && extractedName != "[Unknown]") ? extractedName : uiaName;
        nameToMap = nameToMap?.Trim();
        await _mappingManager.PromptAndSaveMappingAsync(nameToMap, cancellationToken);
    }

    async Task ShowMessageBoxAsync(string message, string title, MessageBoxButton button,
        MessageBoxImage icon, CancellationToken token)
    {
        if(token.IsCancellationRequested) return;

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if(token.IsCancellationRequested || isDisposed ||
               Application.Current == null || Application.Current.Dispatcher.HasShutdownStarted) return;

            _dialogService.ShowMessageBox(message, title, button, icon);
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
                if(_windowsHookService != null)
                {
                    _windowsHookService.RightMouseClick -= OnRightMouseClick;
                    _windowsHookService.Dispose();
                }
                _knobManager?.Dispose();
            }
            isDisposed = true;
        }
    }
}