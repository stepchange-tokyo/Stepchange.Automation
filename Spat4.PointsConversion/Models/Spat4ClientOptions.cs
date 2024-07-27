namespace Spat4.PointsConversion.Models;

public class Spat4ClientOptions
{
    public const string Spat4Client = nameof(Spat4Client);

    public required Uri BaseAddress { get; init; }
    public required int RequestTimeoutInSeconds { get; init; }
    public required int MinConversionWaitTimeInMilliseconds { get; init; }
    public required int MaxConversionWaitTimeInMilliseconds { get; init; }
    public required int MaxConversionsPerAccountPerDay { get; init; }
    public required int MinimumConversionAmount { get; init; }
    public required string PointsHTMLInputValue { get; init; }
}