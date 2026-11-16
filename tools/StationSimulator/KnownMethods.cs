using MarstekOverlay.LocalApi;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace MarstekOverlay.StationSimulator;

/// <summary>The method names the options accept: those of <see cref="MarstekMethods"/>.</summary>
public static class KnownMethods
{
    public static IReadOnlyList<string> All { get; } =
    [
        .. typeof(MarstekMethods)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => field.GetRawConstantValue())
            .OfType<string>(),
    ];

    /// <summary>Matches regardless of case, as the app's <c>Polling:DisabledMethods</c> does, and gives the wire
    /// name, which the station matches exactly.</summary>
    public static bool TryFind(string name, [NotNullWhen(true)] out string? method)
    {
        method = All.FirstOrDefault(known => known.Equals(name, StringComparison.OrdinalIgnoreCase));
        return method is not null;
    }

    public static string Unknown(string name) => $"Unknown method {name}; expected one of {string.Join(", ", All)}.";
}
