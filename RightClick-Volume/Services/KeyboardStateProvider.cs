using System.Windows.Input;
using RightClickVolume.Interfaces;
namespace RightClickVolume.Services;

public class KeyboardStateProvider : IKeyboardStateProvider
{
    public bool IsCtrlPressed() => (Keyboard.GetKeyStates(Key.LeftCtrl) & KeyStates.Down) > 0 || (Keyboard.GetKeyStates(Key.RightCtrl) & KeyStates.Down) > 0;
    public bool IsAltPressed() => (Keyboard.GetKeyStates(Key.LeftAlt) & KeyStates.Down) > 0 || (Keyboard.GetKeyStates(Key.RightAlt) & KeyStates.Down) > 0;
    public bool IsShiftPressed() => (Keyboard.GetKeyStates(Key.LeftShift) & KeyStates.Down) > 0 || (Keyboard.GetKeyStates(Key.RightShift) & KeyStates.Down) > 0;
    public bool IsWinPressed() => (Keyboard.GetKeyStates(Key.LWin) & KeyStates.Down) > 0 || (Keyboard.GetKeyStates(Key.RWin) & KeyStates.Down) > 0;
}