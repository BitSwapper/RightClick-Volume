using System;
using Microsoft.Extensions.DependencyInjection;
using RightClickVolume.Interfaces;
using RightClickVolume.ViewModels;

namespace RightClickVolume.Services;

public class ViewModelFactory : IViewModelFactory
{
    readonly IServiceProvider _serviceProvider;

    public ViewModelFactory(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    public SettingsViewModel CreateSettingsViewModel() => _serviceProvider.GetRequiredService<SettingsViewModel>();

    public AddMappingViewModel CreateAddMappingViewModel(string initialUiaName)
    {
        var dialogService = _serviceProvider.GetRequiredService<IDialogService>();
        return new AddMappingViewModel(initialUiaName, dialogService);
    }

    public ProcessSelectorViewModel CreateProcessSelectorViewModel() => _serviceProvider.GetRequiredService<ProcessSelectorViewModel>();

    public VolumeKnobViewModel CreateVolumeKnobViewModel() => _serviceProvider.GetRequiredService<VolumeKnobViewModel>();
}