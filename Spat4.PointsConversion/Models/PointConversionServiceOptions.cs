namespace Spat4.PointsConversion.Models;

public class PointConversionServiceOptions
{
    public const string PointConversionService = nameof(PointConversionService);

    public required TimeOnly DailyConversionTimeInJst { get; init; }
    public required int MinConversionStartDelayInMilliseconds { get; init; }
    public required int MaxConversionStartDelayInMilliseconds { get; init; }
}