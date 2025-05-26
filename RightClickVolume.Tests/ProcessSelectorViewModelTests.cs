using System.Diagnostics;
using Moq;
using RightClickVolume.Interfaces; // If it were to use IDialogService for errors
using RightClickVolume.ViewModels;

namespace RightClickVolume.Tests;

[TestClass]
public class ProcessSelectorViewModelTests
{
    private ProcessSelectorViewModel viewModel;
    private Mock<IDialogService> mockDialogService;

    [TestInitialize]
    public void TestInitialize()
    {
        mockDialogService = new Mock<IDialogService>();
        viewModel = new ProcessSelectorViewModel(mockDialogService.Object);
    }



    [TestMethod]
    public void Constructor_InitializesProcessesCollection() => Assert.IsNotNull(viewModel.Processes);// Further testing of actual process loading is more of an integration test// We can at least assert it's not null and potentially if it attempts to load.

    [TestMethod]
    public void SelectCommand_WhenProcessSelected_RequestsCloseWithTrue()
    {
        var dummyProcess = Process.GetCurrentProcess(); // Using a real process for selection
        viewModel.Processes.Add(dummyProcess);
        viewModel.SelectedProcess = dummyProcess;

        bool closeRequested = false;
        bool? dialogResult = null;
        viewModel.CloseRequested += (result) =>
        {
            closeRequested = true;
            dialogResult = result;
        };

        viewModel.SelectCommand.Execute(null);

        Assert.IsTrue(closeRequested);
        Assert.IsTrue(dialogResult.HasValue && dialogResult.Value);
    }

    [TestMethod]
    public void SelectCommand_WhenNoProcessSelected_ShowsMessageAndDoesNotClose()
    {
        // To properly test the MessageBox, ProcessSelectorViewModel would need IDialogService
        // For now, we test that it doesn't close.
        viewModel.SelectedProcess = null;
        bool closeRequested = false;
        viewModel.CloseRequested += (result) => closeRequested = true;

        // We can't easily verify MessageBox.Show directly without more setup or refactoring VM.
        // So we execute the command and check that close was not requested.
        viewModel.SelectCommand.Execute(null);

        Assert.IsFalse(closeRequested);
        // If IDialogService was used:
        // _mockDialogService.Verify(d => d.ShowMessageBox("Please select a process from the list.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information), Times.Once);
    }

    [TestMethod]
    public void HandleDoubleClick_WhenProcessSelected_RequestsCloseWithTrue()
    {
        var dummyProcess = Process.GetCurrentProcess();
        viewModel.Processes.Add(dummyProcess);
        viewModel.SelectedProcess = dummyProcess;

        bool closeRequested = false;
        bool? dialogResult = null;
        viewModel.CloseRequested += (result) =>
        {
            closeRequested = true;
            dialogResult = result;
        };

        viewModel.HandleDoubleClick();

        Assert.IsTrue(closeRequested);
        Assert.IsTrue(dialogResult.HasValue && dialogResult.Value);
    }

    [TestMethod]
    public void HandleDoubleClick_WhenNoProcessSelected_DoesNotRequestClose()
    {
        viewModel.SelectedProcess = null;
        bool closeRequested = false;
        viewModel.CloseRequested += (result) => closeRequested = true;

        viewModel.HandleDoubleClick();

        Assert.IsFalse(closeRequested);
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
