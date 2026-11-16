using System.Globalization;

namespace MarstekOverlay.LocalApi.Exceptions;

public class MarstekRequestTimeoutException(StationEndpoint station, string method, TimeSpan timeout)
    : MarstekApiException(string.Create(
        CultureInfo.InvariantCulture,
        $"{station} did not answer {method} within {timeout.TotalSeconds:0.#} s."))
{
    public string Method { get; } = method;
    public TimeSpan Timeout { get; } = timeout;
}
