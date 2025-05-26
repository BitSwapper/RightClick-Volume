using System.Collections.Generic;

namespace RightClickVolume.Models;
public class MappingEntry
{
    public string UiaName { get; set; }
    public List<string> ProcessNames { get; set; } = new List<string>();

    // This property is convenient for display in the ListView
    public string ProcessNameList => string.Join("; ", ProcessNames);
}