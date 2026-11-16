using MarstekOverlay.LocalApi;
using MarstekOverlay.Monitoring.Alerts;
using MarstekOverlay.Monitoring.Forecast;
using MarstekOverlay.Monitoring.Settings;

namespace MarstekOverlay.UnitTests.TestSupport;

internal sealed class FakePipelineSettings : IPipelineSettings
{
    public event Action? Changed;

    public StationEndpoint? Station { get; set; }

    public ForecastSettings Forecast { get; } = new();

    public AlertSettings Alerts { get; } = new();

    public void Change(Action<FakePipelineSettings> change)
    {
        change(this);
        Changed?.Invoke();
    }
}
