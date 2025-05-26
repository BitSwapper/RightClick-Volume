using System.Threading;
using System.Windows;
using System.Windows.Automation;

namespace RightClickVolume.Interfaces;

public interface IUiaScannerService
{
    bool IsInitialized { get; }
    AutomationElement FindElementFromPoint(Point clickPoint, CancellationToken cancellationToken);
    AutomationElement FindTaskbarElement(AutomationElement startElement, CancellationToken cancellationToken);
}