using System.Windows.Media;
using Moq;
using RightClickVolume.Interfaces;
using RightClickVolume.ViewModels;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace RightClickVolume.Tests;

[TestClass]
public class VolumeKnobViewModelTests
{
    private VolumeKnobViewModel viewModel;
    private Mock<IAppAudioSession> mockAppAudioSession;

    private const string IconMuted = "🔇";
    private const string IconUnMuted = "🔊";

    private static readonly Color Color_BG_Muted = Color.FromRgb(0x80, 0x30, 0x30);
    private static readonly Color Color_FG_Muted = Colors.White;
    private static readonly Color Color_BG_UnMuted = Colors.Transparent;
    private static readonly Color Color_FG_UnMuted = Color.FromRgb(0xCC, 0xCC, 0xCC);


    [TestInitialize]
    public void TestInitialize()
    {
        viewModel = new VolumeKnobViewModel();
        mockAppAudioSession = new Mock<IAppAudioSession>();

        mockAppAudioSession.SetupGet(s => s.Volume).Returns(0.5f);
        mockAppAudioSession.SetupGet(s => s.IsMuted).Returns(false);
        mockAppAudioSession.SetupGet(s => s.DisplayName).Returns("TestApp");
    }

    private void InitializeViewModelWithMockSession() => viewModel.InitializeSession(mockAppAudioSession.Object);

    private Color GetBrushColor(Brush brush)
    {
        if(brush is SolidColorBrush scb)
        {
            return scb.Color;
        }
        if(brush == Brushes.Transparent)
        {
            return Colors.Transparent;
        }
        return Colors.Black;
    }


    [TestMethod]
    public void InitializeSession_SetsViewModelProperties()
    {
        mockAppAudioSession.SetupGet(s => s.Volume).Returns(0.7f);
        mockAppAudioSession.SetupGet(s => s.IsMuted).Returns(false);
        mockAppAudioSession.SetupGet(s => s.DisplayName).Returns("SpecificApp");

        InitializeViewModelWithMockSession();

        Assert.AreEqual("SpecificApp", viewModel.AppName);
        Assert.AreEqual(70f, viewModel.Volume, 0.01f);
        Assert.AreEqual("70", viewModel.CurVol);
        Assert.IsFalse(viewModel.IsMuted);
        Assert.AreEqual(IconUnMuted, viewModel.MuteButtonContent);
        Assert.IsTrue(viewModel.IsVolumeSliderEnabled);
        Assert.AreEqual(Color_BG_UnMuted, GetBrushColor(viewModel.MuteButtonBackground));
        Assert.AreEqual(Color_FG_UnMuted, GetBrushColor(viewModel.MuteButtonForeground));
    }

    [TestMethod]
    public void VolumeProperty_WhenSet_UpdatesCurVolAndSessionVolume()
    {
        InitializeViewModelWithMockSession();
        viewModel.Volume = 30f;

        Assert.AreEqual(30f, viewModel.Volume, 0.01f);
        Assert.AreEqual("30", viewModel.CurVol);
        mockAppAudioSession.Verify(s => s.SetVolume(It.Is<float>(v => Math.Abs(v - 0.30f) < 0.001f)), Times.Once);
    }

    [TestMethod]
    public void VolumeProperty_WhenSetToValueNeedingClamping_ClampsAndUpdates()
    {
        InitializeViewModelWithMockSession();
        viewModel.Volume = 150f;

        Assert.AreEqual(100f, viewModel.Volume, 0.01f);
        Assert.AreEqual("100", viewModel.CurVol);
        mockAppAudioSession.Verify(s => s.SetVolume(It.Is<float>(v => Math.Abs(v - 1.0f) < 0.001f)), Times.Once);

        viewModel.Volume = -50f;
        Assert.AreEqual(0f, viewModel.Volume, 0.01f);
        Assert.AreEqual("0", viewModel.CurVol);
        mockAppAudioSession.Verify(s => s.SetVolume(It.Is<float>(v => Math.Abs(v - 0.0f) < 0.001f)), Times.Once);
        mockAppAudioSession.Verify(s => s.SetVolume(It.IsAny<float>()), Times.Exactly(2));
    }

    [TestMethod]
    public void VolumeProperty_WhenSessionMutedAndVolumeSetAboveZero_UnmutesSession()
    {
        mockAppAudioSession.SetupGet(s => s.IsMuted).Returns(true);
        mockAppAudioSession.Setup(s => s.SetMute(false)).Callback(() => mockAppAudioSession.SetupGet(m => m.IsMuted).Returns(false));

        InitializeViewModelWithMockSession();

        Assert.IsTrue(viewModel.IsMuted);
        Assert.IsFalse(viewModel.IsVolumeSliderEnabled);

        viewModel.Volume = 25f;

        mockAppAudioSession.Verify(s => s.SetMute(false), Times.Once);
        Assert.IsFalse(viewModel.IsMuted);
        Assert.AreEqual(IconUnMuted, viewModel.MuteButtonContent);
        Assert.IsTrue(viewModel.IsVolumeSliderEnabled);
        Assert.AreEqual(Color_BG_UnMuted, GetBrushColor(viewModel.MuteButtonBackground));
        Assert.AreEqual(Color_FG_UnMuted, GetBrushColor(viewModel.MuteButtonForeground));
    }

    [TestMethod]
    public void MuteCommand_WhenUnmuted_MutesSessionAndUpdatesUI()
    {
        mockAppAudioSession.SetupGet(s => s.IsMuted).Returns(false);
        mockAppAudioSession.Setup(s => s.SetMute(true)).Callback(() => mockAppAudioSession.SetupGet(m => m.IsMuted).Returns(true));
        InitializeViewModelWithMockSession();

        viewModel.MuteCommand.Execute(null);

        mockAppAudioSession.Verify(s => s.SetMute(true), Times.Once);
        Assert.IsTrue(viewModel.IsMuted);
        Assert.AreEqual(IconMuted, viewModel.MuteButtonContent);
        Assert.AreEqual(Color_BG_Muted, GetBrushColor(viewModel.MuteButtonBackground));
        Assert.AreEqual(Color_FG_Muted, GetBrushColor(viewModel.MuteButtonForeground));
        Assert.IsFalse(viewModel.IsVolumeSliderEnabled);
    }

    [TestMethod]
    public void MuteCommand_WhenMuted_UnmutesSessionAndUpdatesUI()
    {
        mockAppAudioSession.SetupGet(s => s.IsMuted).Returns(true);
        mockAppAudioSession.Setup(s => s.SetMute(false)).Callback(() => mockAppAudioSession.SetupGet(m => m.IsMuted).Returns(false));

        InitializeViewModelWithMockSession();

        viewModel.MuteCommand.Execute(null);

        mockAppAudioSession.Verify(s => s.SetMute(false), Times.Once);
        Assert.IsFalse(viewModel.IsMuted);
        Assert.AreEqual(IconUnMuted, viewModel.MuteButtonContent);
        Assert.AreEqual(Color_BG_UnMuted, GetBrushColor(viewModel.MuteButtonBackground));
        Assert.AreEqual(Color_FG_UnMuted, GetBrushColor(viewModel.MuteButtonForeground));
        Assert.IsTrue(viewModel.IsVolumeSliderEnabled);
    }

    [TestMethod]
    public void UpdateMuteState_WhenSessionIsMuted_UpdatesViewModelPropertiesCorrectly()
    {
        mockAppAudioSession.SetupGet(s => s.IsMuted).Returns(true);
        InitializeViewModelWithMockSession();

        Assert.IsTrue(viewModel.IsMuted);
        Assert.AreEqual(IconMuted, viewModel.MuteButtonContent);
        Assert.AreEqual(Color_BG_Muted, GetBrushColor(viewModel.MuteButtonBackground));
        Assert.AreEqual(Color_FG_Muted, GetBrushColor(viewModel.MuteButtonForeground));
        Assert.IsFalse(viewModel.IsVolumeSliderEnabled);
    }

    [TestMethod]
    public void UpdateMuteState_WhenSessionIsUnmuted_UpdatesViewModelPropertiesCorrectly()
    {
        mockAppAudioSession.SetupGet(s => s.IsMuted).Returns(false);
        InitializeViewModelWithMockSession();

        Assert.IsFalse(viewModel.IsMuted);
        Assert.AreEqual(IconUnMuted, viewModel.MuteButtonContent);
        Assert.AreEqual(Color_BG_UnMuted, GetBrushColor(viewModel.MuteButtonBackground));
        Assert.AreEqual(Color_FG_UnMuted, GetBrushColor(viewModel.MuteButtonForeground));
        Assert.IsTrue(viewModel.IsVolumeSliderEnabled);
    }

    [TestMethod]
    public void CloseCommand_RaisesRequestCloseEvent()
    {
        InitializeViewModelWithMockSession();
        bool eventRaised = false;
        viewModel.RequestClose += () => eventRaised = true;

        viewModel.CloseCommand.Execute(null);

        Assert.IsTrue(eventRaised);
    }
}