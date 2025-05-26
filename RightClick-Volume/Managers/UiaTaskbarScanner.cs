using System;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using RightClickVolume.Interfaces;

namespace RightClickVolume.Managers;

internal class UiaTaskbarScanner : IUiaScannerService
{
    readonly AutomationElement rootElement;
    readonly TreeWalker controlViewWalker;

    public UiaTaskbarScanner()
    {
        try
        {
            rootElement = AutomationElement.RootElement;
            controlViewWalker = TreeWalker.ControlViewWalker;
        }
        catch
        {
            rootElement = null;
            controlViewWalker = null;
        }
    }

    public bool IsInitialized => rootElement != null && controlViewWalker != null;

    public AutomationElement FindElementFromPoint(Point clickPoint, CancellationToken cancellationToken)
    {
        if(!IsInitialized || cancellationToken.IsCancellationRequested) return null;

        try
        {
            return AutomationElement.FromPoint(clickPoint);
        }
        catch(OperationCanceledException) { throw; }
        catch
        {
            return null;
        }
    }

    public AutomationElement FindTaskbarElement(AutomationElement startElement, CancellationToken cancellationToken)
    {
        if(!IsInitialized || startElement == null || cancellationToken.IsCancellationRequested) return null;

        AutomationElement currentElement = startElement;
        while(currentElement != null && currentElement != rootElement && !cancellationToken.IsCancellationRequested)
        {
            var controlType = UiaHelper.GetControlTypeSafe(currentElement);
            if(controlType == ControlType.Button || controlType == ControlType.ListItem)
            {
                if(IsDescendantOfTaskbar(currentElement))
                    return currentElement;
            }


            try
            {
                currentElement = controlViewWalker.GetParent(currentElement);
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    bool IsDescendantOfTaskbar(AutomationElement element)
    {
        if(element == null || controlViewWalker == null) return false;

        AutomationElement ancestor = element;
        int maxDepth = 10;

        try
        {
            for(int i = 0; i < maxDepth && ancestor != null && ancestor != rootElement; i++)
            {
                string className = UiaHelper.GetClassNameSafe(ancestor);
                if(className == "Shell_TrayWnd" || className == "Shell_SecondaryTrayWnd" || className?.StartsWith("TaskListWnd") == true)
                    return true;

                ancestor = controlViewWalker.GetParent(ancestor);
            }
        }
        catch { }
        return false;
    }
}