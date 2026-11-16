using System.Globalization;

namespace MarstekOverlay.LocalApi.Exceptions;

/// <param name="reason">The station's own words for the error.</param>
public class MarstekRpcException(string method, int code, string reason)
    : MarstekApiException(string.Create(CultureInfo.InvariantCulture, $"{method} failed: {reason} (code {code})."))
{
    public const int MethodNotFoundCode = -32601;

    public string Method { get; } = method;
    public int Code { get; } = code;
    public string Reason { get; } = reason;

    /// <summary>How a station says that its model or firmware lacks the method; not a fault.</summary>
    public bool IsMethodNotFound => Code == MethodNotFoundCode;
}
