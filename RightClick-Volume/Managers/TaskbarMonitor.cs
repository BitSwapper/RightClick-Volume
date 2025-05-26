using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Threading;
using RightClickVolume.Interfaces;
using RightClickVolume.Models;


namespace RightClickVolume.Managers;

public class TaskbarMonitor : ITaskbarMonitor
{
    const string ERROR_TITLE = "Error";
    const string AUDIO_SESSION_TITLE = "Audio Session Not Found";

    private readonly IAudioManager audioManager;
    private readonly IUiaScannerService uiaScannerService;
    private readonly IProcessIdentifier processIdentifier;
    private readonly IMappingManager mappingManager;
    private readonly IVolumeKnobManager knobManager;
    private readonly IDialogService dialogService;
    private readonly IHotkeyService hotkeyService;

    private CancellationTokenSource processingCts;
    private long isProcessingClick = 0;
    private bool isDisposed = false;

    public TaskbarMonitor(
        IAudioManager audioManager,
        IMappingManager mappingManager,
        IDialogService dialogService,
        IUiaScannerService uiaScannerService,
        IProcessIdentifier processIdentifier,
        IVolumeKnobManager volumeKnobManager,
        IHotkeyService hotkeyService)
    {
        this.audioManager = audioManager ?? throw new ArgumentNullException(nameof(audioManager));
        this.mappingManager = mappingManager ?? throw new ArgumentNullException(nameof(mappingManager));
        this.dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        this.uiaScannerService = uiaScannerService ?? throw new ArgumentNullException(nameof(uiaScannerService));
        this.processIdentifier = processIdentifier ?? throw new ArgumentNullException(nameof(processIdentifier));
        knobManager = volumeKnobManager ?? throw new ArgumentNullException(nameof(volumeKnobManager));
        this.hotkeyService = hotkeyService ?? throw new ArgumentNullException(nameof(hotkeyService));
    }

    public void StartMonitoring()
    {
        if(isDisposed) throw new ObjectDisposedException(nameof(TaskbarMonitor));

        hotkeyService.GlobalHotkeyPressed += OnHotkeyPressedAsync;
        hotkeyService.StartMonitoring();
        knobManager.StartCleanupTask();
    }

    public void StopMonitoring()
    {
        if(isDisposed) return;

        processingCts?.Cancel();
        hotkeyService.StopMonitoring();
        hotkeyService.GlobalHotkeyPressed -= OnHotkeyPressedAsync;
        knobManager.StopCleanupTask();
        knobManager.HideAllKnobs();
    }

    private async void OnHotkeyPressedAsync(object sender, GlobalHotkeyPressedEventArgs e)
    {
        if(isDisposed || !uiaScannerService.IsInitialized) return;
        if(Interlocked.CompareExchange(ref isProcessingClick, 1, 0) != 0) return;

        processingCts?.Cancel();
        processingCts = new CancellationTokenSource();
        CancellationToken token = processingCts.Token;

        knobManager.HideAllKnobs();

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
            Interlocked.Exchange(ref isProcessingClick, 0);
            processingCts.Dispose();
            processingCts = null;
        }
    }

    async Task ProcessRightClickAsync(int clickX, int clickY, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var clickPoint = new Point(clickX, clickY);
            AutomationElement clickedElement = uiaScannerService.FindElementFromPoint(clickPoint, cancellationToken);
            if(clickedElement == null)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] No UIA element found at click point.");
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            AutomationElement taskbarElement = uiaScannerService.FindTaskbarElement(clickedElement, cancellationToken);
            AutomationElement targetElement = taskbarElement ?? clickedElement;

            cancellationToken.ThrowIfCancellationRequested();
            string uiaName = UiaHelper.GetElementNameSafe(targetElement);
            string extractedName = UiaHelper.ExtractAppNameFromTaskbarUiaName(uiaName);
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Identified UIA Name: '{uiaName}', Extracted: '{extractedName}'");


            cancellationToken.ThrowIfCancellationRequested();
            var identificationResult = processIdentifier.IdentifyProcess(targetElement, extractedName, cancellationToken);

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
        AppAudioSession session = audioManager.GetAudioSessionForProcess(identificationResult.ProcessId) as AppAudioSession;

        if(session != null)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Audio session found for PID {identificationResult.ProcessId}. Showing knob.");
            knobManager.ShowKnobForSession(clickX, clickY, session);
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
            dialogService.ShowMessageBox(message, title, button, icon);
        }, DispatcherPriority.Normal, token);
    }

    public void Dispose()
    {
        if(!isDisposed)
        {
            StopMonitoring();
            processingCts?.Dispose();
            hotkeyService?.Dispose();
            knobManager?.Dispose();
            isDisposed = true;
        }
    }
}