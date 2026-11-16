namespace MarstekOverlay.Monitoring;

/// <param name="StationAnswered">Whether any request of the poll got an answer, a refusal included; without one
/// the requests after the first were not sent.</param>
/// <param name="IdentityUnanswered">Whether the station answered the poll but not the request for its identity,
/// so that the readings were decoded without knowing the model.</param>
public sealed record PollResult(StationSnapshot Snapshot, bool StationAnswered, bool IdentityUnanswered);
