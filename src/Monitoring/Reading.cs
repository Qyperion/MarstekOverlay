namespace MarstekOverlay.Monitoring;

public sealed record Reading<T>(T Value, DateTimeOffset ReceivedAt);
