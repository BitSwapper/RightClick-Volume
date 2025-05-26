using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows; // For MessageBoxResult if error messages were tested via IDialogService
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using RightClickVolume.Interfaces; // If it were to use IDialogService for errors
using RightClickVolume.ViewModels;

namespace RightClickVolume.Tests;

[TestClass]
public class ProcessSelectorViewModelTests
{
    private ProcessSelectorViewModel _viewModel;
    private Mock<IDialogService> _mockDialogService;

    [TestInitialize]
    public void TestInitialize()
    {
        _mockDialogService = new Mock<IDialogService>();
        _viewModel = new ProcessSelectorViewModel(_mockDialogService.Object);
    }



    [TestMethod]
    public void Constructor_InitializesProcessesCollection()
    {
        Assert.IsNotNull(_viewModel.Processes);
        // Further testing of actual process loading is more of an integration test
        // We can at least assert it's not null and potentially if it attempts to load.
    }

    [TestMethod]
    public void SelectCommand_WhenProcessSelected_RequestsCloseWithTrue()
    {
        var dummyProcess = Process.GetCurrentProcess(); // Using a real process for selection
        _viewModel.Processes.Add(dummyProcess);
        _viewModel.SelectedProcess = dummyProcess;

        bool closeRequested = false;
        bool? dialogResult = null;
        _viewModel.CloseRequested += (result) => {
            closeRequested = true;
            dialogResult = result;
        };

        _viewModel.SelectCommand.Execute(null);

        Assert.IsTrue(closeRequested);
        Assert.IsTrue(dialogResult.HasValue && dialogResult.Value);
    }

    [TestMethod]
    public void SelectCommand_WhenNoProcessSelected_ShowsMessageAndDoesNotClose()
    {
        // To properly test the MessageBox, ProcessSelectorViewModel would need IDialogService
        // For now, we test that it doesn't close.
        _viewModel.SelectedProcess = null;
        bool closeRequested = false;
        _viewModel.CloseRequested += (result) => closeRequested = true;

        // We can't easily verify MessageBox.Show directly without more setup or refactoring VM.
        // So we execute the command and check that close was not requested.
        _viewModel.SelectCommand.Execute(null);

        Assert.IsFalse(closeRequested);
        // If IDialogService was used:
        // _mockDialogService.Verify(d => d.ShowMessageBox("Please select a process from the list.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information), Times.Once);
    }

    [TestMethod]
    public void HandleDoubleClick_WhenProcessSelected_RequestsCloseWithTrue()
    {
        var dummyProcess = Process.GetCurrentProcess();
        _viewModel.Processes.Add(dummyProcess);
        _viewModel.SelectedProcess = dummyProcess;

        bool closeRequested = false;
        bool? dialogResult = null;
        _viewModel.CloseRequested += (result) => {
            closeRequested = true;
            dialogResult = result;
        };

        _viewModel.HandleDoubleClick();

        Assert.IsTrue(closeRequested);
        Assert.IsTrue(dialogResult.HasValue && dialogResult.Value);
    }

    [TestMethod]
    public void HandleDoubleClick_WhenNoProcessSelected_DoesNotRequestClose()
    {
        _viewModel.SelectedProcess = null;
        bool closeRequested = false;
        _viewModel.CloseRequested += (result) => closeRequested = true;

        _viewModel.HandleDoubleClick();

        Assert.IsFalse(closeRequested);
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
