using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Threading;
using RightClickVolume.Interfaces;
using RightClickVolume.Models;
using RightClickVolume.Native;


namespace RightClickVolume.Managers;

public class TaskbarMonitor : ITaskbarMonitor
{
    const string ERROR_TITLE = "Error";
    const string AUDIO_SESSION_TITLE = "Audio Session Not Found";

    private readonly IAudioManager _audioManager;
    private readonly IUiaScannerService _uiaScannerService;
    private readonly IProcessIdentifier _processIdentifier;
    private readonly IMappingManager _mappingManager;
    private readonly IVolumeKnobManager _knobManager;
    private readonly IDialogService _dialogService;
    private readonly IHotkeyService _hotkeyService;

    private CancellationTokenSource _processingCts;
    private long _isProcessingClick = 0;
    private bool _isDisposed = false;

    public TaskbarMonitor(
        IAudioManager audioManager,
        IMappingManager mappingManager,
        IDialogService dialogService,
        IUiaScannerService uiaScannerService,
        IProcessIdentifier processIdentifier,
        IVolumeKnobManager volumeKnobManager,
        IHotkeyService hotkeyService)
    {
        _audioManager = audioManager ?? throw new ArgumentNullException(nameof(audioManager));
        _mappingManager = mappingManager ?? throw new ArgumentNullException(nameof(mappingManager));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _uiaScannerService = uiaScannerService ?? throw new ArgumentNullException(nameof(uiaScannerService));
        _processIdentifier = processIdentifier ?? throw new ArgumentNullException(nameof(processIdentifier));
        _knobManager = volumeKnobManager ?? throw new ArgumentNullException(nameof(volumeKnobManager));
        _hotkeyService = hotkeyService ?? throw new ArgumentNullException(nameof(hotkeyService));
    }

    public void StartMonitoring()
    {
        if(_isDisposed) throw new ObjectDisposedException(nameof(TaskbarMonitor));

        _hotkeyService.GlobalHotkeyPressed += OnHotkeyPressedAsync;
        _hotkeyService.StartMonitoring();
        _knobManager.StartCleanupTask();
    }

    public void StopMonitoring()
    {
        if(_isDisposed) return;

        _processingCts?.Cancel();
        _hotkeyService.StopMonitoring();
        _hotkeyService.GlobalHotkeyPressed -= OnHotkeyPressedAsync;
        _knobManager.StopCleanupTask();
        _knobManager.HideAllKnobs();
    }

    private async void OnHotkeyPressedAsync(object sender, GlobalHotkeyPressedEventArgs e)
    {
        if(_isDisposed || !_uiaScannerService.IsInitialized) return;
        if(Interlocked.CompareExchange(ref _isProcessingClick, 1, 0) != 0) return;

        _processingCts?.Cancel();
        _processingCts = new CancellationTokenSource();
        CancellationToken token = _processingCts.Token;

        _knobManager.HideAllKnobs();

        try
        {
            await Task.Run(async () => await ProcessRightClickAsync(e.X, e.Y, token), token);
        }
        catch(OperationCanceledException)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Hotkey processing was canceled.");
        }
        catch(Exception ex)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Unhandled exception in OnHotkeyPressedAsync: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _isProcessingClick, 0);
            _processingCts.Dispose();
            _processingCts = null;
        }
    }

    async Task ProcessRightClickAsync(int clickX, int clickY, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var clickPoint = new Point(clickX, clickY);
            AutomationElement clickedElement = _uiaScannerService.FindElementFromPoint(clickPoint, cancellationToken);
            if(clickedElement == null)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] No UIA element found at click point.");
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            AutomationElement taskbarElement = _uiaScannerService.FindTaskbarElement(clickedElement, cancellationToken);
            AutomationElement targetElement = taskbarElement ?? clickedElement;

            cancellationToken.ThrowIfCancellationRequested();
            string uiaName = UiaHelper.GetElementNameSafe(targetElement);
            string extractedName = UiaHelper.ExtractAppNameFromTaskbarUiaName(uiaName);
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Identified UIA Name: '{uiaName}', Extracted: '{extractedName}'");


            cancellationToken.ThrowIfCancellationRequested();
            var identificationResult = _processIdentifier.IdentifyProcess(targetElement, extractedName, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            if(identificationResult.Success)
            {
                await HandleSuccessfulIdentification(clickX, clickY, identificationResult, cancellationToken);
            }
            else
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Process identification failed for UIA Name: '{uiaName}'.");
                await HandleFailedIdentification(uiaName, extractedName, cancellationToken);
            }
        }
        catch(OperationCanceledException)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ProcessRightClickAsync canceled.");
        }
        catch(Exception ex)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error in ProcessRightClickAsync: {ex.Message}");
            await ShowMessageBoxAsync($"An unexpected error occurred during processing: {ex.Message}", ERROR_TITLE, MessageBoxButton.OK, MessageBoxImage.Error, cancellationToken);
        }
    }

    async Task HandleSuccessfulIdentification(int clickX, int clickY, ProcessIdentifier.IdentificationResult identificationResult,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AppAudioSession session = _audioManager.GetAudioSessionForProcess(identificationResult.ProcessId);

        if(session != null)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Audio session found for PID {identificationResult.ProcessId}. Showing knob.");
            _knobManager.ShowKnobForSession(clickX, clickY, session);
        }
        else
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] No audio session for PID {identificationResult.ProcessId}.");
            string noSessionMessage = $"Found target process '{identificationResult.ApplicationName}' " + $"(PID {identificationResult.ProcessId}, Method: {identificationResult.Method}), " + $"but it does not appear to have an active audio session.\n\nVolume knob cannot be shown.";
            await ShowMessageBoxAsync(noSessionMessage, AUDIO_SESSION_TITLE,
                MessageBoxButton.OK, MessageBoxImage.Information, cancellationToken);
        }
    }

    async Task HandleFailedIdentification(string uiaName, string extractedName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
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
            if(token.IsCancellationRequested || _isDisposed ||
               Application.Current == null || Application.Current.Dispatcher.HasShutdownStarted) return;
            _dialogService.ShowMessageBox(message, title, button, icon);
        }, DispatcherPriority.Normal, token);
    }

    public void Dispose()
    {
        if(!_isDisposed)
        {
            StopMonitoring();
            _processingCts?.Dispose();
            _hotkeyService?.Dispose();
            _knobManager?.Dispose();
            _isDisposed = true;
        }
    }
}