using MarstekOverlay.LocalApi.Exceptions;

namespace MarstekOverlay.StationSimulator;

/// <summary>The JSON-RPC errors a station answers with.</summary>
public static class RpcErrors
{
    public const int ParseError = -32700;
    public const int InvalidRequest = -32600;
    public const int MethodNotFound = MarstekRpcException.MethodNotFoundCode;
    public const int InvalidParams = -32602;
    public const int InternalError = -32603;

    // What Venus E 3.0 puts in "data" when it lacks a method.
    public const int MissingMethodData = 424;

    public static string Message(int code) => code switch
    {
        ParseError => "Parse error",
        InvalidRequest => "Invalid Request",
        MethodNotFound => "Method not found",
        InvalidParams => "Invalid params",
        InternalError => "Internal error",
        _ => "Server error",
    };
}
