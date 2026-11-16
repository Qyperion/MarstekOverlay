using MarstekOverlay.Monitoring.Supply;

namespace MarstekOverlay.TestSupport;

public static class Supplies
{
    public static PowerSupply OnMains(DateTimeOffset since, bool isSinceObserved = true) =>
        new(PowerSource.Mains, since, isSinceObserved);

    public static PowerSupply OnBattery(DateTimeOffset since, bool isSinceObserved = true) =>
        new(PowerSource.Battery, since, isSinceObserved);
}
