using System.Globalization;

namespace MarstekOverlay.StationSimulator;

public enum TimelineAction
{
    Outage,
    Mains,
    Silent,
    Refuse,
    Answer,
    Offline,
    Online,
}

/// <param name="After">Real time since the simulator started.</param>
/// <param name="Method">The wire name the action applies to, for the actions about one method.</param>
/// <param name="Code">The error code a refusal answers with.</param>
public sealed record TimelineEvent(TimeSpan After, TimelineAction Action, string? Method = null, int Code = RpcErrors.InvalidParams)
{
    /// <summary>Reads <c>seconds:action[:method[:code]]</c>, for example <c>60:outage</c> or
    /// <c>90:refuse:ES.GetMode:-32603</c>.</summary>
    public static bool TryParse(string text, out TimelineEvent timelineEvent)
    {
        timelineEvent = new TimelineEvent(TimeSpan.Zero, TimelineAction.Mains);
        var parts = text.Split(':');

        if (parts.Length < 2
            || !double.TryParse(parts[0], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var seconds)
            || !(seconds <= SimulatorOptions.LongestRunSeconds)
            || !EnumNames.TryParse<TimelineAction>(parts[1], out var action))
            return false;

        var isAboutAMethod = action is TimelineAction.Silent or TimelineAction.Refuse or TimelineAction.Answer;
        if (isAboutAMethod != parts.Length >= 3 || parts.Length > (action == TimelineAction.Refuse ? 4 : 3))
            return false;

        var code = RpcErrors.InvalidParams;
        if (parts.Length == 4 && !int.TryParse(parts[3], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out code))
            return false;

        timelineEvent = new TimelineEvent(TimeSpan.FromSeconds(seconds), action, isAboutAMethod ? parts[2] : null, code);
        return true;
    }
}
