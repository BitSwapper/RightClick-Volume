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
    private VolumeKnobViewModel _viewModel;
    private Mock<IAppAudioSession> _mockAppAudioSession;

    private const string IconMuted = "🔇";
    private const string IconUnMuted = "🔊";

    private static readonly Color Color_BG_Muted = Color.FromRgb(0x80, 0x30, 0x30);
    private static readonly Color Color_FG_Muted = Colors.White;
    private static readonly Color Color_BG_UnMuted = Colors.Transparent;
    private static readonly Color Color_FG_UnMuted = Color.FromRgb(0xCC, 0xCC, 0xCC);


    [TestInitialize]
    public void TestInitialize()
    {
        _viewModel = new VolumeKnobViewModel();
        _mockAppAudioSession = new Mock<IAppAudioSession>();

        _mockAppAudioSession.SetupGet(s => s.Volume).Returns(0.5f);
        _mockAppAudioSession.SetupGet(s => s.IsMuted).Returns(false);
        _mockAppAudioSession.SetupGet(s => s.DisplayName).Returns("TestApp");
    }

    private void InitializeViewModelWithMockSession()
    {
        _viewModel.InitializeSession(_mockAppAudioSession.Object);
    }

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
        _mockAppAudioSession.SetupGet(s => s.Volume).Returns(0.7f);
        _mockAppAudioSession.SetupGet(s => s.IsMuted).Returns(false);
        _mockAppAudioSession.SetupGet(s => s.DisplayName).Returns("SpecificApp");

        InitializeViewModelWithMockSession();

        Assert.AreEqual("SpecificApp", _viewModel.AppName);
        Assert.AreEqual(70f, _viewModel.Volume, 0.01f);
        Assert.AreEqual("70", _viewModel.CurVol);
        Assert.IsFalse(_viewModel.IsMuted);
        Assert.AreEqual(IconUnMuted, _viewModel.MuteButtonContent);
        Assert.IsTrue(_viewModel.IsVolumeSliderEnabled);
        Assert.AreEqual(Color_BG_UnMuted, GetBrushColor(_viewModel.MuteButtonBackground));
        Assert.AreEqual(Color_FG_UnMuted, GetBrushColor(_viewModel.MuteButtonForeground));
    }

    [TestMethod]
    public void VolumeProperty_WhenSet_UpdatesCurVolAndSessionVolume()
    {
        InitializeViewModelWithMockSession();
        _viewModel.Volume = 30f;

        Assert.AreEqual(30f, _viewModel.Volume, 0.01f);
        Assert.AreEqual("30", _viewModel.CurVol);
        _mockAppAudioSession.Verify(s => s.SetVolume(It.Is<float>(v => Math.Abs(v - 0.30f) < 0.001f)), Times.Once);
    }

    [TestMethod]
    public void VolumeProperty_WhenSetToValueNeedingClamping_ClampsAndUpdates()
    {
        InitializeViewModelWithMockSession();
        _viewModel.Volume = 150f;

        Assert.AreEqual(100f, _viewModel.Volume, 0.01f);
        Assert.AreEqual("100", _viewModel.CurVol);
        _mockAppAudioSession.Verify(s => s.SetVolume(It.Is<float>(v => Math.Abs(v - 1.0f) < 0.001f)), Times.Once);

        _viewModel.Volume = -50f;
        Assert.AreEqual(0f, _viewModel.Volume, 0.01f);
        Assert.AreEqual("0", _viewModel.CurVol);
        _mockAppAudioSession.Verify(s => s.SetVolume(It.Is<float>(v => Math.Abs(v - 0.0f) < 0.001f)), Times.Once);
        _mockAppAudioSession.Verify(s => s.SetVolume(It.IsAny<float>()), Times.Exactly(2));
    }

    [TestMethod]
    public void VolumeProperty_WhenSessionMutedAndVolumeSetAboveZero_UnmutesSession()
    {
        _mockAppAudioSession.SetupGet(s => s.IsMuted).Returns(true);
        _mockAppAudioSession.Setup(s => s.SetMute(false)).Callback(() => _mockAppAudioSession.SetupGet(m => m.IsMuted).Returns(false));

        InitializeViewModelWithMockSession();

        Assert.IsTrue(_viewModel.IsMuted);
        Assert.IsFalse(_viewModel.IsVolumeSliderEnabled);

        _viewModel.Volume = 25f;

        _mockAppAudioSession.Verify(s => s.SetMute(false), Times.Once);
        Assert.IsFalse(_viewModel.IsMuted);
        Assert.AreEqual(IconUnMuted, _viewModel.MuteButtonContent);
        Assert.IsTrue(_viewModel.IsVolumeSliderEnabled);
        Assert.AreEqual(Color_BG_UnMuted, GetBrushColor(_viewModel.MuteButtonBackground));
        Assert.AreEqual(Color_FG_UnMuted, GetBrushColor(_viewModel.MuteButtonForeground));
    }

    [TestMethod]
    public void MuteCommand_WhenUnmuted_MutesSessionAndUpdatesUI()
    {
        _mockAppAudioSession.SetupGet(s => s.IsMuted).Returns(false);
        _mockAppAudioSession.Setup(s => s.SetMute(true)).Callback(() => _mockAppAudioSession.SetupGet(m => m.IsMuted).Returns(true));
        InitializeViewModelWithMockSession();

        _viewModel.MuteCommand.Execute(null);

        _mockAppAudioSession.Verify(s => s.SetMute(true), Times.Once);
        Assert.IsTrue(_viewModel.IsMuted);
        Assert.AreEqual(IconMuted, _viewModel.MuteButtonContent);
        Assert.AreEqual(Color_BG_Muted, GetBrushColor(_viewModel.MuteButtonBackground));
        Assert.AreEqual(Color_FG_Muted, GetBrushColor(_viewModel.MuteButtonForeground));
        Assert.IsFalse(_viewModel.IsVolumeSliderEnabled);
    }

    [TestMethod]
    public void MuteCommand_WhenMuted_UnmutesSessionAndUpdatesUI()
    {
        _mockAppAudioSession.SetupGet(s => s.IsMuted).Returns(true);
        _mockAppAudioSession.Setup(s => s.SetMute(false)).Callback(() => _mockAppAudioSession.SetupGet(m => m.IsMuted).Returns(false));

        InitializeViewModelWithMockSession();

        _viewModel.MuteCommand.Execute(null);

        _mockAppAudioSession.Verify(s => s.SetMute(false), Times.Once);
        Assert.IsFalse(_viewModel.IsMuted);
        Assert.AreEqual(IconUnMuted, _viewModel.MuteButtonContent);
        Assert.AreEqual(Color_BG_UnMuted, GetBrushColor(_viewModel.MuteButtonBackground));
        Assert.AreEqual(Color_FG_UnMuted, GetBrushColor(_viewModel.MuteButtonForeground));
        Assert.IsTrue(_viewModel.IsVolumeSliderEnabled);
    }

    [TestMethod]
    public void UpdateMuteState_WhenSessionIsMuted_UpdatesViewModelPropertiesCorrectly()
    {
        _mockAppAudioSession.SetupGet(s => s.IsMuted).Returns(true);
        InitializeViewModelWithMockSession();

        Assert.IsTrue(_viewModel.IsMuted);
        Assert.AreEqual(IconMuted, _viewModel.MuteButtonContent);
        Assert.AreEqual(Color_BG_Muted, GetBrushColor(_viewModel.MuteButtonBackground));
        Assert.AreEqual(Color_FG_Muted, GetBrushColor(_viewModel.MuteButtonForeground));
        Assert.IsFalse(_viewModel.IsVolumeSliderEnabled);
    }

    [TestMethod]
    public void UpdateMuteState_WhenSessionIsUnmuted_UpdatesViewModelPropertiesCorrectly()
    {
        _mockAppAudioSession.SetupGet(s => s.IsMuted).Returns(false);
        InitializeViewModelWithMockSession();

        Assert.IsFalse(_viewModel.IsMuted);
        Assert.AreEqual(IconUnMuted, _viewModel.MuteButtonContent);
        Assert.AreEqual(Color_BG_UnMuted, GetBrushColor(_viewModel.MuteButtonBackground));
        Assert.AreEqual(Color_FG_UnMuted, GetBrushColor(_viewModel.MuteButtonForeground));
        Assert.IsTrue(_viewModel.IsVolumeSliderEnabled);
    }

    [TestMethod]
    public void CloseCommand_RaisesRequestCloseEvent()
    {
        InitializeViewModelWithMockSession();
        bool eventRaised = false;
        _viewModel.RequestClose += () => eventRaised = true;

        _viewModel.CloseCommand.Execute(null);

        Assert.IsTrue(eventRaised);
    }
}