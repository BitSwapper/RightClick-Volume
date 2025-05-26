using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using RightClickVolume.Interfaces;
using RightClickVolume.Models;
using RightClickVolume.ViewModels;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;

namespace RightClickVolume.Tests;

[TestClass]
public class SettingsViewModelTests
{
    private Mock<ISettingsService> _mockSettingsService;
    private Mock<IDialogService> _mockDialogService;
    private Mock<IMappingManager> _mockMappingManager;
    private SettingsViewModel _viewModel;

    [TestInitialize]
    public void TestInitialize()
    {
        _mockSettingsService = new Mock<ISettingsService>();
        _mockDialogService = new Mock<IDialogService>();
        _mockMappingManager = new Mock<IMappingManager>();

        _mockSettingsService.Setup(s => s.LaunchOnStartup).Returns(false);
        _mockSettingsService.Setup(s => s.ShowPeakVolumeBar).Returns(true);
        _mockSettingsService.Setup(s => s.Hotkey_Ctrl).Returns(true);
        _mockSettingsService.Setup(s => s.Hotkey_Alt).Returns(false);
        _mockSettingsService.Setup(s => s.Hotkey_Shift).Returns(false);
        _mockSettingsService.Setup(s => s.Hotkey_Win).Returns(false);
        _mockSettingsService.Setup(s => s.ManualMappings).Returns(new StringCollection());

        _mockMappingManager.Setup(m => m.LoadManualMappings()).Returns(new Dictionary<string, List<string>>());


        _viewModel = new SettingsViewModel(_mockSettingsService.Object, _mockDialogService.Object, _mockMappingManager.Object);
    }

    [TestMethod]
    public void Constructor_LoadsInitialSettingsCorrectly()
    {
        _mockSettingsService.Setup(s => s.LaunchOnStartup).Returns(true);
        _mockSettingsService.Setup(s => s.ShowPeakVolumeBar).Returns(false);
        _mockSettingsService.Setup(s => s.Hotkey_Ctrl).Returns(false);

        _viewModel = new SettingsViewModel(_mockSettingsService.Object, _mockDialogService.Object, _mockMappingManager.Object);

        Assert.IsTrue(_viewModel.LaunchOnStartup);
        Assert.IsFalse(_viewModel.ShowPeakVolumeBar);
        Assert.IsFalse(_viewModel.HotkeyCtrl);
    }

    [TestMethod]
    public void LoadMappings_PopulatesMappingsCollection()
    {
        var mappingsData = new Dictionary<string, List<string>>
        {
            { "UIA1", new List<string> { "Proc1.exe", "Proc2.exe" } },
            { "UIA2", new List<string> { "Proc3.exe" } }
        };
        _mockMappingManager.Setup(m => m.LoadManualMappings()).Returns(mappingsData);

        _viewModel = new SettingsViewModel(_mockSettingsService.Object, _mockDialogService.Object, _mockMappingManager.Object);

        Assert.AreEqual(2, _viewModel.Mappings.Count);
        Assert.AreEqual("UIA1", _viewModel.Mappings[0].UiaName);
        Assert.AreEqual(2, _viewModel.Mappings[0].ProcessNames.Count);
        Assert.AreEqual("Proc3.exe", _viewModel.Mappings[1].ProcessNames[0]);
    }

    [TestMethod]
    public void SaveCommand_WhenValid_SavesSettingsAndCloses()
    {
        _viewModel.HotkeyCtrl = true;
        bool closeRequested = false;
        bool? dialogResult = null;
        _viewModel.CloseRequested += (result) => {
            closeRequested = true;
            dialogResult = result;
        };

        _viewModel.SaveCommand.Execute(null);

        _mockSettingsService.Verify(s => s.Save(), Times.Once);
        Assert.IsTrue(closeRequested);
        Assert.IsTrue(dialogResult.HasValue && dialogResult.Value);
    }

    [TestMethod]
    public void SaveCommand_InvalidHotkeys_ShowsErrorAndDoesNotSave()
    {
        _viewModel.HotkeyCtrl = false;
        _viewModel.HotkeyAlt = false;
        _viewModel.HotkeyShift = false;
        _viewModel.HotkeyWin = false;

        _viewModel.SaveCommand.Execute(null);

        _mockDialogService.Verify(d => d.ShowMessageBox(It.IsAny<string>(), "Invalid Hotkey", MessageBoxButton.OK, MessageBoxImage.Warning), Times.Once);
        _mockSettingsService.Verify(s => s.Save(), Times.Never);
    }

    [TestMethod]
    public void AddEditCommand_DialogOk_SavesMappingAndReloads()
    {
        _mockDialogService.Setup(d => d.ShowAddMappingWindow(null))
                          .Returns((true, "NewUIA", "NewProc.exe"));
        _mockMappingManager.Setup(m => m.SaveOrUpdateManualMapping("NewUIA", "NewProc.exe"))
                           .Returns(true);

        var initialMappingsForThisTest = new Dictionary<string, List<string>>();
        var updatedMappingsAfterSave = new Dictionary<string, List<string>> { { "NewUIA", new List<string> { "NewProc.exe" } } };

        _mockMappingManager.SetupSequence(m => m.LoadManualMappings())
                           .Returns(initialMappingsForThisTest)
                           .Returns(updatedMappingsAfterSave);

        _viewModel = new SettingsViewModel(_mockSettingsService.Object, _mockDialogService.Object, _mockMappingManager.Object);

        Assert.AreEqual(0, _viewModel.Mappings.Count, "Initial mappings count should be 0 for this test setup.");

        _viewModel.AddEditCommand.Execute(null);

        _mockMappingManager.Verify(m => m.SaveOrUpdateManualMapping("NewUIA", "NewProc.exe"), Times.Once);
        _mockDialogService.Verify(d => d.ShowMessageBox(It.IsAny<string>(), "Mapping Saved", MessageBoxButton.OK, MessageBoxImage.Information), Times.Once);

        System.Diagnostics.Debug.WriteLine($"Mappings count after AddEdit: {_viewModel.Mappings.Count}");
        if(_viewModel.Mappings.Count > 0)
        {
            System.Diagnostics.Debug.WriteLine($"First mapping UIA Name: {_viewModel.Mappings[0].UiaName}");
        }

        Assert.AreEqual(1, _viewModel.Mappings.Count, "Mappings count should be 1 after adding one.");
        if(_viewModel.Mappings.Count > 0)
        {
            Assert.AreEqual("NewUIA", _viewModel.Mappings[0].UiaName);
        }
    }

    [TestMethod]
    public void RemoveCommand_WithSelectionAndYesConfirmation_RemovesMapping()
    {
        var mappingToRemove = new MappingEntry { UiaName = "TestUIA", ProcessNames = new List<string> { "TestProc.exe" } };
        _viewModel.Mappings.Add(mappingToRemove);
        _viewModel.SelectedMapping = mappingToRemove;
        _mockDialogService.Setup(d => d.ShowMessageBox(It.IsAny<string>(), "Confirm Removal", MessageBoxButton.YesNo, MessageBoxImage.Warning))
                          .Returns(MessageBoxResult.Yes);

        _viewModel.RemoveCommand.Execute(null);

        Assert.IsFalse(_viewModel.Mappings.Contains(mappingToRemove));
        Assert.IsNull(_viewModel.SelectedMapping);
    }

    [TestMethod]
    public void RemoveCommand_WithSelectionAndNoConfirmation_DoesNotRemoveMapping()
    {
        var mappingToRemove = new MappingEntry { UiaName = "TestUIA", ProcessNames = new List<string> { "TestProc.exe" } };
        _viewModel.Mappings.Add(mappingToRemove);
        _viewModel.SelectedMapping = mappingToRemove;
        _mockDialogService.Setup(d => d.ShowMessageBox(It.IsAny<string>(), "Confirm Removal", MessageBoxButton.YesNo, MessageBoxImage.Warning))
                          .Returns(MessageBoxResult.No);

        _viewModel.RemoveCommand.Execute(null);

        Assert.IsTrue(_viewModel.Mappings.Contains(mappingToRemove));
        Assert.AreEqual(mappingToRemove, _viewModel.SelectedMapping);
    }

    [TestMethod]
    public void CancelCommand_RequestsCloseWithFalse()
    {
        bool closeRequested = false;
        bool? dialogResult = null;
        _viewModel.CloseRequested += (result) => {
            closeRequested = true;
            dialogResult = result;
        };

        _viewModel.CancelCommand.Execute(null);

        Assert.IsTrue(closeRequested);
        Assert.IsTrue(dialogResult.HasValue && !dialogResult.Value);
    }
}