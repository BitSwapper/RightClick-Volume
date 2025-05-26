namespace RightClickVolume.Interfaces;

public interface IKeyboardStateProvider
{
    bool IsCtrlPressed();
    bool IsAltPressed();
    bool IsShiftPressed();
    bool IsWinPressed();
}