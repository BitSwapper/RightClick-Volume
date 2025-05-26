using System;
using System.Windows.Input;
using RightClickVolume.Interfaces;
using RightClickVolume.Native;

namespace RightClickVolume.Services;

public class HotkeyService : IHotkeyService
{
    private readonly IWindowsHookService _windowsHookService;
    private readonly ISettingsService _settingsService;
    private bool _isMonitoring = false;

    private bool _reqCtrl;
    private bool _reqAlt;
    private bool _reqShift;
    private bool _reqWin;

    public event EventHandler<GlobalHotkeyPressedEventArgs> GlobalHotkeyPressed;

    public HotkeyService(IWindowsHookService windowsHookService, ISettingsService settingsService)
    {
        _windowsHookService = windowsHookService ?? throw new ArgumentNullException(nameof(windowsHookService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        LoadHotkeySettings();
        _settingsService.PropertyChanged += OnSettingsChanged;
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
        _reqCtrl = _settingsService.Hotkey_Ctrl;
        _reqAlt = _settingsService.Hotkey_Alt;
        _reqShift = _settingsService.Hotkey_Shift;
        _reqWin = _settingsService.Hotkey_Win;
    }

    public void StartMonitoring()
    {
        if(_isMonitoring) return;
        _windowsHookService.RightMouseClick += OnGlobalRightMouseClick;
        _windowsHookService.InstallMouseHook();
        _isMonitoring = true;
    }

    public void StopMonitoring()
    {
        if(!_isMonitoring) return;
        _windowsHookService.UninstallMouseHook();
        _windowsHookService.RightMouseClick -= OnGlobalRightMouseClick;
        _isMonitoring = false;
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
        bool ctrlPressed = (Keyboard.GetKeyStates(Key.LeftCtrl) & KeyStates.Down) > 0 ||
                           (Keyboard.GetKeyStates(Key.RightCtrl) & KeyStates.Down) > 0;
        bool altPressed = (Keyboard.GetKeyStates(Key.LeftAlt) & KeyStates.Down) > 0 ||
                          (Keyboard.GetKeyStates(Key.RightAlt) & KeyStates.Down) > 0;
        bool shiftPressed = (Keyboard.GetKeyStates(Key.LeftShift) & KeyStates.Down) > 0 ||
                            (Keyboard.GetKeyStates(Key.RightShift) & KeyStates.Down) > 0;
        bool winPressed = (Keyboard.GetKeyStates(Key.LWin) & KeyStates.Down) > 0 ||
                          (Keyboard.GetKeyStates(Key.RWin) & KeyStates.Down) > 0;

        bool hotkeyMatch = (ctrlPressed == _reqCtrl) &&
                           (altPressed == _reqAlt) &&
                           (shiftPressed == _reqShift) &&
                           (winPressed == _reqWin);

        bool anyModifierRequired = _reqCtrl || _reqAlt || _reqShift || _reqWin;

        return hotkeyMatch && anyModifierRequired;
    }

    public void Dispose()
    {
        StopMonitoring();
        _settingsService.PropertyChanged -= OnSettingsChanged;
    }
}