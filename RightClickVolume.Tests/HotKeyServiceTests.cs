using System.ComponentModel;
using Moq;
using RightClickVolume.Interfaces;
using RightClickVolume.Native;
using RightClickVolume.Services;

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

        hotkeyService = new HotkeyService(
            mockWindowsHookService.Object,
            mockSettingsService.Object,
            mockKeyboardStateProvider.Object);
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
    public void OnGlobalRightMouseClick_WhenCorrectModifiersPressed_RaisesGlobalHotkeyPressed()
    {
        mockSettingsService.Setup(s => s.Hotkey_Ctrl).Returns(true);
        mockSettingsService.Setup(s => s.Hotkey_Alt).Returns(false);
        mockSettingsService.Setup(s => s.Hotkey_Shift).Returns(false);
        mockSettingsService.Setup(s => s.Hotkey_Win).Returns(false);

        mockKeyboardStateProvider.Setup(ksp => ksp.IsCtrlPressed()).Returns(true);
        mockKeyboardStateProvider.Setup(ksp => ksp.IsAltPressed()).Returns(false);
        mockKeyboardStateProvider.Setup(ksp => ksp.IsShiftPressed()).Returns(false);
        mockKeyboardStateProvider.Setup(ksp => ksp.IsWinPressed()).Returns(false);

        bool eventRaised = false;
        GlobalHotkeyPressedEventArgs eventArgs = null;
        hotkeyService.GlobalHotkeyPressed += (sender, args) =>
        {
            eventRaised = true;
            eventArgs = args;
        };

        hotkeyService.StartMonitoring();

        var mouseArgs = new MouseHookEventArgs { X = 100, Y = 200, WindowHandle = (IntPtr)123 };
        mockWindowsHookService.Raise(whs => whs.RightMouseClick += null, mouseArgs);

        Assert.IsTrue(eventRaised, "GlobalHotkeyPressed event was not raised.");
        Assert.IsNotNull(eventArgs, "Event args should not be null.");
        Assert.AreEqual(100, eventArgs.X);
        Assert.AreEqual(200, eventArgs.Y);
        Assert.AreEqual((IntPtr)123, eventArgs.WindowHandle);
    }

    [TestMethod]
    public void OnGlobalRightMouseClick_WhenIncorrectModifiersPressed_DoesNotRaiseGlobalHotkeyPressed()
    {
        mockSettingsService.Setup(s => s.Hotkey_Ctrl).Returns(true);
        mockSettingsService.Setup(s => s.Hotkey_Alt).Returns(false);

        mockKeyboardStateProvider.Setup(ksp => ksp.IsCtrlPressed()).Returns(false);
        mockKeyboardStateProvider.Setup(ksp => ksp.IsAltPressed()).Returns(true);
        mockKeyboardStateProvider.Setup(ksp => ksp.IsShiftPressed()).Returns(false);
        mockKeyboardStateProvider.Setup(ksp => ksp.IsWinPressed()).Returns(false);

        bool eventRaised = false;
        hotkeyService.GlobalHotkeyPressed += (sender, args) => eventRaised = true;

        hotkeyService.StartMonitoring();

        mockWindowsHookService.Raise(whs => whs.RightMouseClick += null, new MouseHookEventArgs());

        Assert.IsFalse(eventRaised, "GlobalHotkeyPressed event should not have been raised.");
    }

    [TestMethod]
    public void OnGlobalRightMouseClick_WhenNoModifiersRequiredAndAnyPressed_DoesNotRaiseGlobalHotkeyPressed()
    {
        mockSettingsService.Setup(s => s.Hotkey_Ctrl).Returns(false);
        mockSettingsService.Setup(s => s.Hotkey_Alt).Returns(false);
        mockSettingsService.Setup(s => s.Hotkey_Shift).Returns(false);
        mockSettingsService.Setup(s => s.Hotkey_Win).Returns(false);
        var serviceInstance = (HotkeyService)hotkeyService;
        mockSettingsService.Raise(s => s.PropertyChanged += null, new PropertyChangedEventArgs(nameof(ISettingsService.Hotkey_Ctrl)));


        mockKeyboardStateProvider.Setup(ksp => ksp.IsCtrlPressed()).Returns(true);
        mockKeyboardStateProvider.Setup(ksp => ksp.IsAltPressed()).Returns(false);

        bool eventRaised = false;
        hotkeyService.GlobalHotkeyPressed += (sender, args) => eventRaised = true;

        hotkeyService.StartMonitoring();

        mockWindowsHookService.Raise(whs => whs.RightMouseClick += null, new MouseHookEventArgs());

        Assert.IsFalse(eventRaised, "GlobalHotkeyPressed event should not fire if no modifiers are required by settings.");
    }

    [TestMethod]
    public void SettingsChanged_UpdatesInternalRequiredModifiers()
    {
        mockSettingsService.Setup(s => s.Hotkey_Alt).Returns(true);
        mockSettingsService.Raise(s => s.PropertyChanged += null, new PropertyChangedEventArgs(nameof(ISettingsService.Hotkey_Alt)));

        mockKeyboardStateProvider.Setup(ksp => ksp.IsCtrlPressed()).Returns(true);
        mockKeyboardStateProvider.Setup(ksp => ksp.IsAltPressed()).Returns(true);
        mockKeyboardStateProvider.Setup(ksp => ksp.IsShiftPressed()).Returns(false);
        mockKeyboardStateProvider.Setup(ksp => ksp.IsWinPressed()).Returns(false);

        bool eventRaised = false;
        hotkeyService.GlobalHotkeyPressed += (sender, args) => eventRaised = true;

        hotkeyService.StartMonitoring();
        mockWindowsHookService.Raise(whs => whs.RightMouseClick += null, new MouseHookEventArgs());

        Assert.IsTrue(eventRaised, "Event should be raised after settings update and correct key press.");
    }

    [TestMethod]
    public void Dispose_StopsMonitoringAndUnsubscribesFromSettings()
    {
        hotkeyService.StartMonitoring();
        hotkeyService.Dispose();

        mockWindowsHookService.Verify(whs => whs.UninstallMouseHook(), Times.Once);
    }
}