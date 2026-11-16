using FluentAssertions;

using MarstekOverlay.Monitoring.History;

using Xunit;

namespace MarstekOverlay.UnitTests.MonitoringTests;

public class KeptPeriodTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 12, 0, 0, TimeSpan.FromHours(2));

    [Theory]
    [InlineData(KeptPeriod.Day, 1)]
    [InlineData(KeptPeriod.Week, 7)]
    [InlineData(KeptPeriod.Month, 30)]
    public void StartsAt_PeriodKept_BeginsThatManyDaysBack(KeptPeriod period, int days)
    {
        period.StartsAt(Now).Should().Be(Now - TimeSpan.FromDays(days));
    }

    [Fact]
    public void StartsAt_NothingKept_NeverBegins()
    {
        KeptPeriod.Nothing.StartsAt(Now).Should().Be(DateTimeOffset.MaxValue);
    }
}
