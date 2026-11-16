using CommunityToolkit.Mvvm.ComponentModel;

namespace MarstekOverlay.Desktop.ViewModels;

/// <summary>One of a set of radio buttons: picking it tells what it stands for, unpicking it tells nothing.</summary>
public sealed partial class ChoiceOption<T>(T choice, Func<T, string> name, bool isSelected, Action<T> onSelected)
    : ObservableObject
{
    public string Name => name(choice);

    [ObservableProperty]
    public partial bool IsSelected { get; set; } = isSelected;

    public void RefreshName() => OnPropertyChanged(nameof(Name));

    // What a screen reader calls the choice.
    public override string ToString() => Name;

    partial void OnIsSelectedChanged(bool value)
    {
        if (value)
            onSelected(choice);
    }
}
