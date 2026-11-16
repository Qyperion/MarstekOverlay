namespace MarstekOverlay.StationSimulator;

internal static class EnumNames
{
    /// <summary>Names only, regardless of case. <see cref="Enum.TryParse{TEnum}(string?, bool, out TEnum)"/> also
    /// takes a number, so <c>--scenario 1</c> would pass for a scenario.</summary>
    public static bool TryParse<TEnum>(string text, out TEnum value)
        where TEnum : struct, Enum
    {
        var name = Enum.GetNames<TEnum>().FirstOrDefault(known => known.Equals(text, StringComparison.OrdinalIgnoreCase));
        value = name is null ? default : Enum.Parse<TEnum>(name);
        return name is not null;
    }
}
