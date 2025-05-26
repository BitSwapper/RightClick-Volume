using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RightClickVolume.Interfaces;

public interface IMappingManager
{
    Dictionary<string, List<string>> LoadManualMappings();
    bool SaveOrUpdateManualMapping(string uiaName, string processNameToAdd);
    Task PromptAndSaveMappingAsync(string uiaNameToMap, CancellationToken cancellationToken);
}