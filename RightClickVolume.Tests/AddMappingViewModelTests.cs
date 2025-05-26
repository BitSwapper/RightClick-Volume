using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using RightClickVolume.Interfaces;
using RightClickVolume.ViewModels;
using System.Diagnostics;
using System.Windows;

namespace RightClickVolume.Tests;

[TestClass]
public class AddMappingViewModelTests
{
    private Mock<IDialogService> _mockDialogService;
    private AddMappingViewModel _viewModel;
    private const string TestUiaName = "TestAppUIA";

    [TestInitialize]
    public void TestInitialize()
    {
        _mockDialogService = new Mock<IDialogService>();
        _viewModel = new AddMappingViewModel(TestUiaName, _mockDialogService.Object);
    }

    [TestMethod]
    public void Constructor_SetsInitialUiaName()
    {
        Assert.AreEqual(TestUiaName, _viewModel.UiaName);
    }

    [TestMethod]
    public void OkCommand_CanExecute_IsFalseInitially()
    {
        Assert.IsFalse(_viewModel.OkCommand.CanExecute(null));
    }

    [TestMethod]
    public void OkCommand_CanExecute_IsTrueWhenUiaNameAndProcessSelected()
    {
        _viewModel.UiaName = "SomeUIA";
        _viewModel.SelectedProcess = Process.GetCurrentProcess();
        Assert.IsTrue(_viewModel.OkCommand.CanExecute(null));
    }

    [TestMethod]
    public void OkCommand_CanExecute_IsFalseWhenUiaNameIsEmpty()
    {
        _viewModel.UiaName = "";
        _viewModel.SelectedProcess = Process.GetCurrentProcess();
        Assert.IsFalse(_viewModel.OkCommand.CanExecute(null));
    }

    [TestMethod]
    public void OkCommand_CanExecute_IsFalseWhenProcessNotSelected()
    {
        _viewModel.UiaName = "SomeUIA";
        _viewModel.SelectedProcess = null;
        Assert.IsFalse(_viewModel.OkCommand.CanExecute(null));
    }

    [TestMethod]
    public void OkCommand_WhenValid_RequestsCloseWithTrue()
    {
        _viewModel.UiaName = "ValidUIA";
        _viewModel.SelectedProcess = Process.GetCurrentProcess();
        bool closeRequested = false;
        bool? dialogResult = null;
        _viewModel.RequestCloseDialog += (result) => {
            closeRequested = true;
            dialogResult = result;
        };

        _viewModel.OkCommand.Execute(null);

        Assert.IsTrue(closeRequested);
        Assert.IsTrue(dialogResult.HasValue && dialogResult.Value);
    }

    [TestMethod]
    public void OkCommand_EmptyUiaName_ShowsErrorAndDoesNotClose()
    {
        _viewModel.UiaName = " ";
        _viewModel.SelectedProcess = Process.GetCurrentProcess();
        bool closeRequested = false;
        _viewModel.RequestCloseDialog += (result) => closeRequested = true;

        _viewModel.OkCommand.Execute(null);

        _mockDialogService.Verify(d => d.ShowMessageBox("UIA Name cannot be empty.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning), Times.Once);
        Assert.IsFalse(closeRequested);
    }

    [TestMethod]
    public void OkCommand_NoProcessSelected_ShowsErrorAndDoesNotClose()
    {
        _viewModel.UiaName = "ValidUIA";
        _viewModel.SelectedProcess = null;
        bool closeRequested = false;
        _viewModel.RequestCloseDialog += (result) => closeRequested = true;

        _viewModel.OkCommand.Execute(null);

        _mockDialogService.Verify(d => d.ShowMessageBox("Please select a target process.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning), Times.Once);
        Assert.IsFalse(closeRequested);
    }

    [TestMethod]
    public void BrowseProcessCommand_WhenDialogReturnsProcess_SetsSelectedProcess()
    {
        var mockProcess = Process.GetCurrentProcess();
        _viewModel.ShowProcessSelectorDialogFunc = () => mockProcess;

        _viewModel.BrowseProcessCommand.Execute(null);

        Assert.AreEqual(mockProcess, _viewModel.SelectedProcess);
        Assert.AreEqual(mockProcess.ProcessName, _viewModel.ProcessNameDisplay);
    }

    [TestMethod]
    public void BrowseProcessCommand_WhenDialogReturnsNull_SelectedProcessUnchanged()
    {
        _viewModel.SelectedProcess = null;
        _viewModel.ShowProcessSelectorDialogFunc = () => null;

        _viewModel.BrowseProcessCommand.Execute(null);

        Assert.IsNull(_viewModel.SelectedProcess);
        Assert.IsNull(_viewModel.ProcessNameDisplay);
    }


    [TestMethod]
    public void CancelCommand_RequestsCloseWithFalse()
    {
        bool closeRequested = false;
        bool? dialogResult = null;
        _viewModel.RequestCloseDialog += (result) => {
            closeRequested = true;
            dialogResult = result;
        };

        _viewModel.CancelCommand.Execute(null);

        Assert.IsTrue(closeRequested);
        Assert.IsTrue(dialogResult.HasValue && !dialogResult.Value);
    }
}