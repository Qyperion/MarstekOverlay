using System.Diagnostics.CodeAnalysis;

namespace MarstekOverlay.Desktop.Services;

public interface IAutostart
{
    bool IsEnabled { get; }

    /// <param name="failure">Why the entry was left as it was.</param>
    bool TrySet(bool isEnabled, [NotNullWhen(false)] out Exception? failure);
}
