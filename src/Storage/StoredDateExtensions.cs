namespace MarstekOverlay.Storage;

internal static class StoredDateExtensions
{
    extension(DateTime storedUtc)
    {
        public DateTimeOffset AsUtcMoment => new(DateTime.SpecifyKind(storedUtc, DateTimeKind.Utc));
    }
}
