using CommunityToolkit.Mvvm.ComponentModel;

using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.LocalApi.Models;

namespace MarstekOverlay.Desktop.ViewModels;

public sealed class DeviceChoice(DiscoveredDevice found) : ObservableObject
{
    public DiscoveredDevice Found { get; } = found;

    public string Name => SettingsMessages.Device(Found);

    public void RefreshName() => OnPropertyChanged(nameof(Name));

    // What a screen reader calls the choice.
    public override string ToString() => Name;
}
