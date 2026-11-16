using CommunityToolkit.Mvvm.ComponentModel;

namespace MarstekOverlay.Desktop.ViewModels;

/// <summary>What is wrong with the fields of a window, by the name of the property a field is bound to. A window
/// reads a message through the indexer, which is announced as a whole whenever any message changes.</summary>
public sealed class FieldErrors(Action<string> announce) : ObservableObject
{
    private const string Indexer = "Item[]";

    private readonly Dictionary<string, string> _messages = [];

    public bool Any => _messages.Count > 0;

    public IReadOnlyCollection<string> Fields => _messages.Keys;

    public string? this[string field] => _messages.GetValueOrDefault(field);

    /// <param name="message">Nothing for a field that is in order.</param>
    public void Set(string field, string? message)
    {
        if (this[field] == message)
            return;

        if (message is null)
            _messages.Remove(field);
        else
            _messages[field] = message;

        OnPropertyChanged(Indexer);
        announce(field);
    }

    public void Clear()
    {
        foreach (var field in _messages.Keys.ToList())
            Set(field, null);
    }
}
