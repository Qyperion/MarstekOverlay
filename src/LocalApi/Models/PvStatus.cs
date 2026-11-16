namespace MarstekOverlay.LocalApi.Models;

public sealed record PvStatus
{
    public IReadOnlyList<PvChannel> Channels { get; init; } = [];

    public Power? TotalPower =>
        Channels.Any(channel => channel.Power is not null)
            ? new Power(Channels.Sum(channel => channel.Power?.Watts ?? 0))
            : null;
}
