using System;
using RightClickVolume.Interfaces;
using RightClickVolume.Native;

namespace RightClickVolume.Services;

public class HotkeyService : IHotkeyService
{
    private readonly IWindowsHookService windowsHookService;
    private readonly ISettingsService settingsService;
    private readonly IKeyboardStateProvider keyboardStateProvider;
    private bool isMonitoring = false;

    private bool reqCtrl;
    private bool reqAlt;
    private bool reqShift;
    private bool reqWin;

    public event EventHandler<GlobalHotkeyPressedEventArgs> GlobalHotkeyPressed;

    public HotkeyService(
        IWindowsHookService windowsHookService,
        ISettingsService settingsService,
        IKeyboardStateProvider keyboardStateProvider)
    {
        this.windowsHookService = windowsHookService ?? throw new ArgumentNullException(nameof(windowsHookService));
        this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        this.keyboardStateProvider = keyboardStateProvider ?? throw new ArgumentNullException(nameof(keyboardStateProvider));

        LoadHotkeySettings();
        this.settingsService.PropertyChanged += OnSettingsChanged;
    }

    private void OnSettingsChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if(e.PropertyName == nameof(ISettingsService.Hotkey_Ctrl) ||
            e.PropertyName == nameof(ISettingsService.Hotkey_Alt) ||
            e.PropertyName == nameof(ISettingsService.Hotkey_Shift) ||
            e.PropertyName == nameof(ISettingsService.Hotkey_Win))
        {
            LoadHotkeySettings();
        }
    }

    private void LoadHotkeySettings()
    {
        reqCtrl = settingsService.Hotkey_Ctrl;
        reqAlt = settingsService.Hotkey_Alt;
        reqShift = settingsService.Hotkey_Shift;
        reqWin = settingsService.Hotkey_Win;
    }

    public void StartMonitoring()
    {
        if(isMonitoring) return;
        windowsHookService.RightMouseClick += OnGlobalRightMouseClick;
        windowsHookService.InstallMouseHook();
        isMonitoring = true;
    }

    public void StopMonitoring()
    {
        if(!isMonitoring) return;
        windowsHookService.UninstallMouseHook();
        windowsHookService.RightMouseClick -= OnGlobalRightMouseClick;
        isMonitoring = false;
    }

    private void OnGlobalRightMouseClick(object sender, MouseHookEventArgs e)
    {
        if(CheckHotkeyModifiers())
        {
            GlobalHotkeyPressed?.Invoke(this, new GlobalHotkeyPressedEventArgs(e.X, e.Y, e.WindowHandle));
        }
    }

    private bool CheckHotkeyModifiers()
    {
        bool ctrlPressed = keyboardStateProvider.IsCtrlPressed();
        bool altPressed = keyboardStateProvider.IsAltPressed();
        bool shiftPressed = keyboardStateProvider.IsShiftPressed();
        bool winPressed = keyboardStateProvider.IsWinPressed();

        bool hotkeyMatch = (ctrlPressed == reqCtrl) &&
                           (altPressed == reqAlt) &&
                           (shiftPressed == reqShift) &&
                           (winPressed == reqWin);

        bool anyModifierRequired = reqCtrl || reqAlt || reqShift || reqWin;

        return hotkeyMatch && anyModifierRequired;
    }

    public void Dispose()
    {
        StopMonitoring();
        if(settingsService != null)
            settingsService.PropertyChanged -= OnSettingsChanged;
    }
}