using System;
using Microsoft.Extensions.DependencyInjection;
using RightClickVolume.Interfaces;
using RightClickVolume.ViewModels;

namespace RightClickVolume.Services;

public class ViewModelFactory : IViewModelFactory
{
    readonly IServiceProvider serviceProvider;

    public ViewModelFactory(IServiceProvider serviceProvider) => this.serviceProvider = serviceProvider;

    public SettingsViewModel CreateSettingsViewModel() => serviceProvider.GetRequiredService<SettingsViewModel>();

    public AddMappingViewModel CreateAddMappingViewModel(string initialUiaName)
    {
        var dialogService = serviceProvider.GetRequiredService<IDialogService>();
        return new AddMappingViewModel(initialUiaName, dialogService);
    }

    public ProcessSelectorViewModel CreateProcessSelectorViewModel() => serviceProvider.GetRequiredService<ProcessSelectorViewModel>();

    public VolumeKnobViewModel CreateVolumeKnobViewModel() => serviceProvider.GetRequiredService<VolumeKnobViewModel>();
}