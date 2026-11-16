namespace MarstekOverlay.Monitoring;

public enum PollScope
{
    /// <summary>Power flows, operating mode, meter and PV inputs.</summary>
    Fast,

    /// <summary>The fast requests after the identity, asked once more because the full poll went without it.</summary>
    FastWithIdentity,

    /// <summary>Everything the station can report.</summary>
    Full,
}
