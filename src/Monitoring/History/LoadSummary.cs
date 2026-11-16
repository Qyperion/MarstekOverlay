using MarstekOverlay.LocalApi.Models;

namespace MarstekOverlay.Monitoring.History;

public readonly record struct LoadSummary(Power Min, Power Average, Power Max);
