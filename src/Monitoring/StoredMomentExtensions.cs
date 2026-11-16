namespace MarstekOverlay.Monitoring;

public static class StoredMomentExtensions
{
    extension(DateTimeOffset moment)
    {
        /// <summary>The moment as a store keeps it and compares it: to the millisecond, the finer part dropped.</summary>
        public DateTimeOffset AsStored => moment.AddTicks(-(moment.Ticks % TimeSpan.TicksPerMillisecond));
    }
}
