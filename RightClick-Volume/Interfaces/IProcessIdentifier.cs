using System.Threading;
using System.Windows.Automation;
using RightClickVolume.Managers;

namespace RightClickVolume.Interfaces;

public interface IProcessIdentifier
{
    ProcessIdentifier.IdentificationResult IdentifyProcess(AutomationElement targetElement, string extractedName, CancellationToken cancellationToken);
}