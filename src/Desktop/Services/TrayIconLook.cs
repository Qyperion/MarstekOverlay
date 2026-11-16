using MarstekOverlay.Desktop.Presentation;

namespace MarstekOverlay.Desktop.Services;

/// <summary>Everything the drawn icon depends on, so that it is drawn again only when it would come out different.</summary>
/// <param name="Size">In pixels; the notification area asks for a larger icon on a scaled-up display.</param>
/// <param name="FilledColumns">How many pixel columns of the battery the charge fills.</param>
public readonly record struct TrayIconLook(int Size, int FilledColumns, Tone ChargeTone, SupplyMark Mark);
