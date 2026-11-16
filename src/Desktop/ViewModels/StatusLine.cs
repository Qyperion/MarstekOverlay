using CommunityToolkit.Mvvm.ComponentModel;

using MarstekOverlay.Desktop.Presentation;

namespace MarstekOverlay.Desktop.ViewModels;

/// <summary>The one line under the settings window's tabs, which every part of the window writes to; the latest
/// word wins.</summary>
public sealed partial class StatusLine : ObservableObject
{
    [ObservableProperty]
    public partial StatusMessage Message { get; set; } = SettingsMessages.ReadOnlyNote;
}
