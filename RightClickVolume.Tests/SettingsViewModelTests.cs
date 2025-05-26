using System.Collections.Specialized;
using System.Windows;
using Moq;
using RightClickVolume.Interfaces;
using RightClickVolume.Models;
using RightClickVolume.ViewModels;

namespace RightClickVolume.Tests;

[TestClass]
public class SettingsViewModelTests
{
    private Mock<ISettingsService> mockSettingsService;
    private Mock<IDialogService> mockDialogService;
    private Mock<IMappingManager> mockMappingManager;
    private SettingsViewModel viewModel;

    [TestInitialize]
    public void TestInitialize()
    {
        mockSettingsService = new Mock<ISettingsService>();
        mockDialogService = new Mock<IDialogService>();
        mockMappingManager = new Mock<IMappingManager>();

        mockSettingsService.Setup(s => s.LaunchOnStartup).Returns(false);
        mockSettingsService.Setup(s => s.ShowPeakVolumeBar).Returns(true);
        mockSettingsService.Setup(s => s.Hotkey_Ctrl).Returns(true);
        mockSettingsService.Setup(s => s.Hotkey_Alt).Returns(false);
        mockSettingsService.Setup(s => s.Hotkey_Shift).Returns(false);
        mockSettingsService.Setup(s => s.Hotkey_Win).Returns(false);
        mockSettingsService.Setup(s => s.ManualMappings).Returns(new StringCollection());

        mockMappingManager.Setup(m => m.LoadManualMappings()).Returns(new Dictionary<string, List<string>>());


        viewModel = new SettingsViewModel(mockSettingsService.Object, mockDialogService.Object, mockMappingManager.Object);
    }

    [TestMethod]
    public void Constructor_LoadsInitialSettingsCorrectly()
    {
        mockSettingsService.Setup(s => s.LaunchOnStartup).Returns(true);
        mockSettingsService.Setup(s => s.ShowPeakVolumeBar).Returns(false);
        mockSettingsService.Setup(s => s.Hotkey_Ctrl).Returns(false);

        viewModel = new SettingsViewModel(mockSettingsService.Object, mockDialogService.Object, mockMappingManager.Object);

        Assert.IsTrue(viewModel.LaunchOnStartup);
        Assert.IsFalse(viewModel.ShowPeakVolumeBar);
        Assert.IsFalse(viewModel.HotkeyCtrl);
    }

    [TestMethod]
    public void LoadMappings_PopulatesMappingsCollection()
    {
        var mappingsData = new Dictionary<string, List<string>>
        {
            { "UIA1", new List<string> { "Proc1.exe", "Proc2.exe" } },
            { "UIA2", new List<string> { "Proc3.exe" } }
        };
        mockMappingManager.Setup(m => m.LoadManualMappings()).Returns(mappingsData);

        viewModel = new SettingsViewModel(mockSettingsService.Object, mockDialogService.Object, mockMappingManager.Object);

        Assert.AreEqual(2, viewModel.Mappings.Count);
        Assert.AreEqual("UIA1", viewModel.Mappings[0].UiaName);
        Assert.AreEqual(2, viewModel.Mappings[0].ProcessNames.Count);
        Assert.AreEqual("Proc3.exe", viewModel.Mappings[1].ProcessNames[0]);
    }

    [TestMethod]
    public void SaveCommand_WhenValid_SavesSettingsAndCloses()
    {
        viewModel.HotkeyCtrl = true;
        bool closeRequested = false;
        bool? dialogResult = null;
        viewModel.CloseRequested += (result) =>
        {
            closeRequested = true;
            dialogResult = result;
        };

        viewModel.SaveCommand.Execute(null);

        mockSettingsService.Verify(s => s.Save(), Times.Once);
        Assert.IsTrue(closeRequested);
        Assert.IsTrue(dialogResult.HasValue && dialogResult.Value);
    }

    [TestMethod]
    public void SaveCommand_InvalidHotkeys_ShowsErrorAndDoesNotSave()
    {
        viewModel.HotkeyCtrl = false;
        viewModel.HotkeyAlt = false;
        viewModel.HotkeyShift = false;
        viewModel.HotkeyWin = false;

        viewModel.SaveCommand.Execute(null);

        mockDialogService.Verify(d => d.ShowMessageBox(It.IsAny<string>(), "Invalid Hotkey", MessageBoxButton.OK, MessageBoxImage.Warning), Times.Once);
        mockSettingsService.Verify(s => s.Save(), Times.Never);
    }

    [TestMethod]
    public void AddEditCommand_DialogOk_SavesMappingAndReloads()
    {
        mockDialogService.Setup(d => d.ShowAddMappingWindow(null))
                          .Returns((true, "NewUIA", "NewProc.exe"));
        mockMappingManager.Setup(m => m.SaveOrUpdateManualMapping("NewUIA", "NewProc.exe"))
                           .Returns(true);

        var initialMappingsForThisTest = new Dictionary<string, List<string>>();
        var updatedMappingsAfterSave = new Dictionary<string, List<string>> { { "NewUIA", new List<string> { "NewProc.exe" } } };

        mockMappingManager.SetupSequence(m => m.LoadManualMappings())
                           .Returns(initialMappingsForThisTest)
                           .Returns(updatedMappingsAfterSave);

        viewModel = new SettingsViewModel(mockSettingsService.Object, mockDialogService.Object, mockMappingManager.Object);

        Assert.AreEqual(0, viewModel.Mappings.Count, "Initial mappings count should be 0 for this test setup.");

        viewModel.AddEditCommand.Execute(null);

        mockMappingManager.Verify(m => m.SaveOrUpdateManualMapping("NewUIA", "NewProc.exe"), Times.Once);
        mockDialogService.Verify(d => d.ShowMessageBox(It.IsAny<string>(), "Mapping Saved", MessageBoxButton.OK, MessageBoxImage.Information), Times.Once);

        System.Diagnostics.Debug.WriteLine($"Mappings count after AddEdit: {viewModel.Mappings.Count}");
        if(viewModel.Mappings.Count > 0)
        {
            System.Diagnostics.Debug.WriteLine($"First mapping UIA Name: {viewModel.Mappings[0].UiaName}");
        }

        Assert.AreEqual(1, viewModel.Mappings.Count, "Mappings count should be 1 after adding one.");
        if(viewModel.Mappings.Count > 0)
        {
            Assert.AreEqual("NewUIA", viewModel.Mappings[0].UiaName);
        }
    }

    [TestMethod]
    public void RemoveCommand_WithSelectionAndYesConfirmation_RemovesMapping()
    {
        var mappingToRemove = new MappingEntry { UiaName = "TestUIA", ProcessNames = new List<string> { "TestProc.exe" } };
        viewModel.Mappings.Add(mappingToRemove);
        viewModel.SelectedMapping = mappingToRemove;
        mockDialogService.Setup(d => d.ShowMessageBox(It.IsAny<string>(), "Confirm Removal", MessageBoxButton.YesNo, MessageBoxImage.Warning))
                          .Returns(MessageBoxResult.Yes);

        viewModel.RemoveCommand.Execute(null);

        Assert.IsFalse(viewModel.Mappings.Contains(mappingToRemove));
        Assert.IsNull(viewModel.SelectedMapping);
    }

    [TestMethod]
    public void RemoveCommand_WithSelectionAndNoConfirmation_DoesNotRemoveMapping()
    {
        var mappingToRemove = new MappingEntry { UiaName = "TestUIA", ProcessNames = new List<string> { "TestProc.exe" } };
        viewModel.Mappings.Add(mappingToRemove);
        viewModel.SelectedMapping = mappingToRemove;
        mockDialogService.Setup(d => d.ShowMessageBox(It.IsAny<string>(), "Confirm Removal", MessageBoxButton.YesNo, MessageBoxImage.Warning))
                          .Returns(MessageBoxResult.No);

        viewModel.RemoveCommand.Execute(null);

        Assert.IsTrue(viewModel.Mappings.Contains(mappingToRemove));
        Assert.AreEqual(mappingToRemove, viewModel.SelectedMapping);
    }

    [TestMethod]
    public void CancelCommand_RequestsCloseWithFalse()
    {
        bool closeRequested = false;
        bool? dialogResult = null;
        viewModel.CloseRequested += (result) =>
        {
            closeRequested = true;
            dialogResult = result;
        };

        viewModel.CancelCommand.Execute(null);

        Assert.IsTrue(closeRequested);
        Assert.IsTrue(dialogResult.HasValue && !dialogResult.Value);
    }
}