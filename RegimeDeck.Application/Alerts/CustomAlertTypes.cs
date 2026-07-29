namespace RegimeDeck.Application.Alerts;

public static class AlertConditionTypes
{
    public const string MacroIndicator = "MacroIndicator";
    public const string AssetPrice = "AssetPrice";
    public const string MovingAverage = "MovingAverage";
    public const string VolumeSpike = "VolumeSpike";

    public static readonly string[] All = [MacroIndicator, AssetPrice, MovingAverage, VolumeSpike];
}

public static class AlertOperators
{
    public const string Above = "Above";
    public const string Below = "Below";

    public static readonly string[] All = [Above, Below];
}
