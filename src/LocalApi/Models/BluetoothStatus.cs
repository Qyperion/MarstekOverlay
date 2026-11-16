namespace MarstekOverlay.LocalApi.Models;

public sealed record BluetoothStatus
{
    private const string ConnectedState = "connect";

    /// <summary>Raw firmware wording; "connect" and "disconnect" are the values seen so far.</summary>
    public string? State { get; init; }

    public string? Mac { get; init; }

    public bool? IsConnected => State is null ? null : string.Equals(State, ConnectedState, StringComparison.OrdinalIgnoreCase);
}
