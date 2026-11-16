namespace MarstekOverlay.Monitoring;

/// <summary>How many records a deletion took away of each kind; nothing for a kind it was not asked to delete.</summary>
public sealed record DeletedRecords(int? Samples, int? Outages);
