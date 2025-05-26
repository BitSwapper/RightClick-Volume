using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using RightClickVolume.Interfaces;
using RightClickVolume.Services;
using RightClickVolume.Native;
using System;
using System.ComponentModel;

namespace RightClickVolume.Tests;

[TestClass]
public class HotkeyServiceTests
{
    private Mock<IWindowsHookService> mockWindowsHookService;
    private Mock<ISettingsService> mockSettingsService;
    private Mock<IKeyboardStateProvider> mockKeyboardStateProvider;
    private HotkeyService hotkeyService;

    [TestInitialize]
    public void TestInitialize()
    {
        mockWindowsHookService = new Mock<IWindowsHookService>();
        mockSettingsService = new Mock<ISettingsService>();
        mockKeyboardStateProvider = new Mock<IKeyboardStateProvider>();

        mockSettingsService.Setup(s => s.Hotkey_Ctrl).Returns(true);
        mockSettingsService.Setup(s => s.Hotkey_Alt).Returns(false);
        mockSettingsService.Setup(s => s.Hotkey_Shift).Returns(false);
        mockSettingsService.Setup(s => s.Hotkey_Win).Returns(false);

        hotkeyService = new HotkeyService(mockWindowsHookService.Object, mockSettingsService.Object, mockKeyboardStateProvider.Object);
    }

    [TestMethod]
    public void StartMonitoring_InstallsHookAndSubscribes()
    {
        hotkeyService.StartMonitoring();

        mockWindowsHookService.Verify(whs => whs.InstallMouseHook(), Times.Once);
    }

    [TestMethod]
    public void StopMonitoring_UninstallsHookAndUnsubscribes()
    {
        hotkeyService.StartMonitoring();
        hotkeyService.StopMonitoring();

        mockWindowsHookService.Verify(whs => whs.UninstallMouseHook(), Times.Once);
    }

    [TestMethod]
    public void StopMonitoring_WhenNotMonitoring_DoesNothingExtra()
    {
        hotkeyService.StopMonitoring();

        mockWindowsHookService.Verify(whs => whs.UninstallMouseHook(), Times.Never);
    }


    [TestMethod]
    public void OnGlobalRightMouseClick_CtrlRequiredAndPressed_RaisesGlobalHotkeyPressed()
    {
        SetupSettings(ctrl: true, alt: false, shift: false, win: false);
        SetupKeyboardState(ctrl: true, alt: false, shift: false, win: false);

        bool eventRaised = false;
        GlobalHotkeyPressedEventArgs eventArgs = null;
        hotkeyService.GlobalHotkeyPressed += (sender, args) => {
            eventRaised = true;
            eventArgs = args;
        };
        hotkeyService.StartMonitoring();

        var mouseArgs = new MouseHookEventArgs { X = 10, Y = 20, WindowHandle = (IntPtr)1 };
        mockWindowsHookService.Raise(whs => whs.RightMouseClick += null, mouseArgs);

        Assert.IsTrue(eventRaised);
        Assert.IsNotNull(eventArgs);
        Assert.AreEqual(10, eventArgs.X);
        Assert.AreEqual(20, eventArgs.Y);
        Assert.AreEqual((IntPtr)1, eventArgs.WindowHandle);
    }

    [TestMethod]
    public void OnGlobalRightMouseClick_CtrlAndAltRequired_CtrlAndAltPressed_RaisesGlobalHotkeyPressed()
    {
        mockSettingsService.Setup(s => s.Hotkey_Ctrl).Returns(true);
        mockSettingsService.Setup(s => s.Hotkey_Alt).Returns(true);
        mockSettingsService.Setup(s => s.Hotkey_Shift).Returns(false);
        mockSettingsService.Setup(s => s.Hotkey_Win).Returns(false);

        mockKeyboardStateProvider.Setup(ksp => ksp.IsCtrlPressed()).Returns(true);
        mockKeyboardStateProvider.Setup(ksp => ksp.IsAltPressed()).Returns(true);
        mockKeyboardStateProvider.Setup(ksp => ksp.IsShiftPressed()).Returns(false);
        mockKeyboardStateProvider.Setup(ksp => ksp.IsWinPressed()).Returns(false);

        hotkeyService = new HotkeyService(mockWindowsHookService.Object, mockSettingsService.Object, mockKeyboardStateProvider.Object);

        bool eventRaised = false;
        hotkeyService.GlobalHotkeyPressed += (sender, args) => eventRaised = true;
        hotkeyService.StartMonitoring();

        mockWindowsHookService.Raise(whs => whs.RightMouseClick += null, new MouseHookEventArgs());

        Assert.IsTrue(eventRaised, "Event should have been raised with Ctrl and Alt required and pressed.");
    }

    [TestMethod]
    public void OnGlobalRightMouseClick_CtrlRequiredButNotPressed_DoesNotRaiseGlobalHotkeyPressed()
    {
        SetupSettings(ctrl: true, alt: false, shift: false, win: false);
        SetupKeyboardState(ctrl: false, alt: false, shift: false, win: false);

        bool eventRaised = false;
        hotkeyService.GlobalHotkeyPressed += (sender, args) => eventRaised = true;
        hotkeyService.StartMonitoring();

        mockWindowsHookService.Raise(whs => whs.RightMouseClick += null, new MouseHookEventArgs());

        Assert.IsFalse(eventRaised);
    }

    [TestMethod]
    public void OnGlobalRightMouseClick_CtrlRequiredAndPressed_AltNotRequiredButPressed_DoesNotRaiseGlobalHotkeyPressed()
    {
        SetupSettings(ctrl: true, alt: false, shift: false, win: false);
        SetupKeyboardState(ctrl: true, alt: true, shift: false, win: false);

        bool eventRaised = false;
        hotkeyService.GlobalHotkeyPressed += (sender, args) => eventRaised = true;
        hotkeyService.StartMonitoring();

        mockWindowsHookService.Raise(whs => whs.RightMouseClick += null, new MouseHookEventArgs());

        Assert.IsFalse(eventRaised);
    }

    [TestMethod]
    public void OnGlobalRightMouseClick_NoModifiersRequiredBySettings_DoesNotRaiseGlobalHotkeyPressed()
    {
        SetupSettings(ctrl: false, alt: false, shift: false, win: false);
        mockSettingsService.Raise(s => s.PropertyChanged += null, new PropertyChangedEventArgs(nameof(ISettingsService.Hotkey_Ctrl)));


        SetupKeyboardState(ctrl: true, alt: false, shift: false, win: false);

        bool eventRaised = false;
        hotkeyService.GlobalHotkeyPressed += (sender, args) => eventRaised = true;
        hotkeyService.StartMonitoring();

        mockWindowsHookService.Raise(whs => whs.RightMouseClick += null, new MouseHookEventArgs());

        Assert.IsFalse(eventRaised, "Event should not fire if settings require no modifiers, as 'anyModifierRequired' will be false.");
    }


    [TestMethod]
    public void SettingsChanged_UpdatesInternalRequiredModifiers_AndReactsCorrectly()
    {
        hotkeyService.StartMonitoring();

        SetupSettings(ctrl: true, alt: true, shift: false, win: false);
        mockSettingsService.Raise(s => s.PropertyChanged += null, new PropertyChangedEventArgs(nameof(ISettingsService.Hotkey_Alt)));

        SetupKeyboardState(ctrl: true, alt: true, shift: false, win: false);

        bool eventRaised = false;
        hotkeyService.GlobalHotkeyPressed += (sender, args) => eventRaised = true;

        mockWindowsHookService.Raise(whs => whs.RightMouseClick += null, new MouseHookEventArgs());

        Assert.IsTrue(eventRaised, "Event should be raised after settings update and correct new key press.");
    }

    [TestMethod]
    public void SettingsChanged_ToNoModifiersRequired_PreventsEvent()
    {
        hotkeyService.StartMonitoring();

        SetupSettings(ctrl: false, alt: false, shift: false, win: false);
        mockSettingsService.Raise(s => s.PropertyChanged += null, new PropertyChangedEventArgs(nameof(ISettingsService.Hotkey_Ctrl)));


        SetupKeyboardState(ctrl: true, alt: false, shift: false, win: false);

        bool eventRaised = false;
        hotkeyService.GlobalHotkeyPressed += (sender, args) => eventRaised = true;

        mockWindowsHookService.Raise(whs => whs.RightMouseClick += null, new MouseHookEventArgs());

        Assert.IsFalse(eventRaised, "Event should not fire if settings changed to require no modifiers.");
    }


    [TestMethod]
    public void Dispose_StopsMonitoringAndUnsubscribesFromSettings()
    {
        hotkeyService.StartMonitoring();
        hotkeyService.Dispose();

        mockWindowsHookService.Verify(whs => whs.UninstallMouseHook(), Times.Once);
    }

    private void SetupSettings(bool ctrl, bool alt, bool shift, bool win)
    {
        mockSettingsService.Setup(s => s.Hotkey_Ctrl).Returns(ctrl);
        mockSettingsService.Setup(s => s.Hotkey_Alt).Returns(alt);
        mockSettingsService.Setup(s => s.Hotkey_Shift).Returns(shift);
        mockSettingsService.Setup(s => s.Hotkey_Win).Returns(win);
    }

    private void SetupKeyboardState(bool ctrl, bool alt, bool shift, bool win)
    {
        mockKeyboardStateProvider.Setup(ksp => ksp.IsCtrlPressed()).Returns(ctrl);
        mockKeyboardStateProvider.Setup(ksp => ksp.IsAltPressed()).Returns(alt);
        mockKeyboardStateProvider.Setup(ksp => ksp.IsShiftPressed()).Returns(shift);
        mockKeyboardStateProvider.Setup(ksp => ksp.IsWinPressed()).Returns(win);
    }
}