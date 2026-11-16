namespace MarstekOverlay.Monitoring;

/// <summary>What the app records of the station and keeps on disk.</summary>
[Flags]
public enum RecordKinds
{
    None = 0,
    Samples = 1,
    Outages = 2,
}
