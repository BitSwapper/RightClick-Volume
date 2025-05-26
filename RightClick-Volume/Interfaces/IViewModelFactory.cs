using RightClickVolume.ViewModels;

namespace RightClickVolume.Interfaces;

public interface IViewModelFactory
{
    SettingsViewModel CreateSettingsViewModel();
    AddMappingViewModel CreateAddMappingViewModel(string initialUiaName);
    ProcessSelectorViewModel CreateProcessSelectorViewModel();
    VolumeKnobViewModel CreateVolumeKnobViewModel();
}