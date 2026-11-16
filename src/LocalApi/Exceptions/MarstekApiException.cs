namespace MarstekOverlay.LocalApi.Exceptions;

public class MarstekApiException : Exception
{
    public MarstekApiException(string message)
        : base(message)
    {
    }

    public MarstekApiException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
