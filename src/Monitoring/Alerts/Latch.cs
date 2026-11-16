namespace MarstekOverlay.Monitoring.Alerts;

/// <summary>Remembers that a condition has been told of, until it clears.</summary>
internal sealed class Latch : IAlertWatch
{
    private bool _isWatched;
    private bool _isSet;

    /// <returns>True when the condition has just set in and is news.</returns>
    public bool Follow(bool isMet, bool isCleared, bool isNewsAtFirstSight)
    {
        var isFirstSight = !_isWatched;
        _isWatched = true;

        if (isCleared)
            _isSet = false;

        if (!isMet || _isSet)
            return false;

        _isSet = true;
        return !isFirstSight || isNewsAtFirstSight;
    }

    public void Forget()
    {
        _isWatched = false;
        _isSet = false;
    }
}
