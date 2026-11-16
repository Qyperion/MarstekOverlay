namespace MarstekOverlay.Monitoring.Supply;

/// <summary>What one status answer tells about the power source.</summary>
public enum SupplyEvidence
{
    None,

    /// <summary>Power crosses the grid port, or its energy counter has moved since the previous answer: the
    /// grid is there beyond doubt.</summary>
    GridAtWork,

    /// <summary>A full battery beside a silent grid port. That is how a charged station on the mains looks,
    /// and an outage in its first minutes, before the charge has dropped, looks no different.</summary>
    FullBattery,

    /// <summary>A battery that is not full gives power while the grid port stays silent. The grid would be
    /// charging it, so the grid is taken to be gone once the next answer says the same.</summary>
    DrainingBattery,
}
