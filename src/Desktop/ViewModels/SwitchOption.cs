using CommunityToolkit.Mvvm.ComponentModel;

namespace MarstekOverlay.Desktop.ViewModels;

/// <summary>A check box that switches one thing of a kind on or off.</summary>
public sealed partial class SwitchOption<T>(T kind, Func<T, string> name, bool isChecked, Action<T, bool> onSwitched)
    : ObservableObject
{
    public string Name => name(kind);

    [ObservableProperty]
    public partial bool IsChecked { get; set; } = isChecked;

    public void RefreshName() => OnPropertyChanged(nameof(Name));

    // What a screen reader calls the choice.
    public override string ToString() => Name;

    partial void OnIsCheckedChanged(bool value) => onSwitched(kind, value);
}
