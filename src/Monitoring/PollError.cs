using MarstekOverlay.LocalApi.Exceptions;

namespace MarstekOverlay.Monitoring;

/// <summary>What went wrong with a request, kept as facts rather than as a sentence, so that it can be told in
/// the language of the app.</summary>
public abstract record PollError
{
    public static PollError From(Exception exception) => exception switch
    {
        MarstekRequestTimeoutException timeout => new UnansweredRequest(timeout.Method, timeout.Timeout),
        MarstekRpcException refusal => new RefusedRequest(refusal.Method, refusal.Code, refusal.Reason),
        _ => new UnexpectedFailure(exception.Message),
    };
}

/// <param name="Method">The wire name of the request.</param>
/// <param name="WaitedFor">How long each attempt was given.</param>
public sealed record UnansweredRequest(string Method, TimeSpan WaitedFor) : PollError;

/// <param name="Method">The wire name of the request.</param>
/// <param name="Reason">The station's own words for the error.</param>
public sealed record RefusedRequest(string Method, int Code, string Reason) : PollError;

/// <summary>Anything rarer, such as a socket that would not open.</summary>
/// <param name="Message">As the system words it.</param>
public sealed record UnexpectedFailure(string Message) : PollError;
