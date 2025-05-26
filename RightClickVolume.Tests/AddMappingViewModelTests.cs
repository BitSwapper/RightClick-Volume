using System.Diagnostics;
using System.Windows;
using Moq;
using RightClickVolume.Interfaces;
using RightClickVolume.ViewModels;

namespace RightClickVolume.Tests;

[TestClass]
public class AddMappingViewModelTests
{
    private Mock<IDialogService> mockDialogService;
    private AddMappingViewModel viewModel;
    private const string TestUiaName = "TestAppUIA";

    [TestInitialize]
    public void TestInitialize()
    {
        mockDialogService = new Mock<IDialogService>();
        viewModel = new AddMappingViewModel(TestUiaName, mockDialogService.Object);
    }

    [TestMethod]
    public void Constructor_SetsInitialUiaName() => Assert.AreEqual(TestUiaName, viewModel.UiaName);

    [TestMethod]
    public void OkCommand_CanExecute_IsFalseInitially() => Assert.IsFalse(viewModel.OkCommand.CanExecute(null));

    [TestMethod]
    public void OkCommand_CanExecute_IsTrueWhenUiaNameAndProcessSelected()
    {
        viewModel.UiaName = "SomeUIA";
        viewModel.SelectedProcess = Process.GetCurrentProcess();
        Assert.IsTrue(viewModel.OkCommand.CanExecute(null));
    }

    [TestMethod]
    public void OkCommand_CanExecute_IsFalseWhenUiaNameIsEmpty()
    {
        viewModel.UiaName = "";
        viewModel.SelectedProcess = Process.GetCurrentProcess();
        Assert.IsFalse(viewModel.OkCommand.CanExecute(null));
    }

    [TestMethod]
    public void OkCommand_CanExecute_IsFalseWhenProcessNotSelected()
    {
        viewModel.UiaName = "SomeUIA";
        viewModel.SelectedProcess = null;
        Assert.IsFalse(viewModel.OkCommand.CanExecute(null));
    }

    [TestMethod]
    public void OkCommand_WhenValid_RequestsCloseWithTrue()
    {
        viewModel.UiaName = "ValidUIA";
        viewModel.SelectedProcess = Process.GetCurrentProcess();
        bool closeRequested = false;
        bool? dialogResult = null;
        viewModel.RequestCloseDialog += (result) =>
        {
            closeRequested = true;
            dialogResult = result;
        };

        viewModel.OkCommand.Execute(null);

        Assert.IsTrue(closeRequested);
        Assert.IsTrue(dialogResult.HasValue && dialogResult.Value);
    }

    [TestMethod]
    public void OkCommand_EmptyUiaName_ShowsErrorAndDoesNotClose()
    {
        viewModel.UiaName = " ";
        viewModel.SelectedProcess = Process.GetCurrentProcess();
        bool closeRequested = false;
        viewModel.RequestCloseDialog += (result) => closeRequested = true;

        viewModel.OkCommand.Execute(null);

        mockDialogService.Verify(d => d.ShowMessageBox("UIA Name cannot be empty.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning), Times.Once);
        Assert.IsFalse(closeRequested);
    }

    [TestMethod]
    public void OkCommand_NoProcessSelected_ShowsErrorAndDoesNotClose()
    {
        viewModel.UiaName = "ValidUIA";
        viewModel.SelectedProcess = null;
        bool closeRequested = false;
        viewModel.RequestCloseDialog += (result) => closeRequested = true;

        viewModel.OkCommand.Execute(null);

        mockDialogService.Verify(d => d.ShowMessageBox("Please select a target process.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning), Times.Once);
        Assert.IsFalse(closeRequested);
    }

    [TestMethod]
    public void BrowseProcessCommand_WhenDialogReturnsProcess_SetsSelectedProcess()
    {
        var mockProcess = Process.GetCurrentProcess();
        viewModel.ShowProcessSelectorDialogFunc = () => mockProcess;

        viewModel.BrowseProcessCommand.Execute(null);

        Assert.AreEqual(mockProcess, viewModel.SelectedProcess);
        Assert.AreEqual(mockProcess.ProcessName, viewModel.ProcessNameDisplay);
    }

    [TestMethod]
    public void BrowseProcessCommand_WhenDialogReturnsNull_SelectedProcessUnchanged()
    {
        viewModel.SelectedProcess = null;
        viewModel.ShowProcessSelectorDialogFunc = () => null;

        viewModel.BrowseProcessCommand.Execute(null);

        Assert.IsNull(viewModel.SelectedProcess);
        Assert.IsNull(viewModel.ProcessNameDisplay);
    }


    [TestMethod]
    public void CancelCommand_RequestsCloseWithFalse()
    {
        bool closeRequested = false;
        bool? dialogResult = null;
        viewModel.RequestCloseDialog += (result) =>
        {
            closeRequested = true;
            dialogResult = result;
        };

        viewModel.CancelCommand.Execute(null);

        Assert.IsTrue(closeRequested);
        Assert.IsTrue(dialogResult.HasValue && !dialogResult.Value);
    }
}